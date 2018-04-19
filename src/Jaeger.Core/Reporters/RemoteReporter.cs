using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jaeger.Core.Exceptions;
using Jaeger.Core.Metrics;
using Jaeger.Core.Senders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jaeger.Core.Reporters
{
    /// <summary>
    /// <see cref="RemoteReporter"/> buffers spans in memory and sends them out of process using <see cref="ISender"/>.
    /// </summary>
    public class RemoteReporter : IReporter
    {
        public const int DefaultMaxQueueSize = 100;
        public static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(1);

        private readonly BlockingCollection<ICommand> _commandQueue;
        private readonly Task _queueProcessor;
        private readonly Timer _flushTimer;
        private readonly ISender _sender;
        private readonly IMetrics _metrics;
        private readonly ILogger _logger;

        internal RemoteReporter(ISender sender, TimeSpan flushInterval, int maxQueueSize,
            IMetrics metrics, ILoggerFactory loggerFactory)
        {
            _sender = sender;
            _metrics = metrics;
            _logger = loggerFactory.CreateLogger<RemoteReporter>();
            _commandQueue = new BlockingCollection<ICommand>(maxQueueSize);

            // start a thread to append spans
            _queueProcessor = Task.Factory.StartNew(ConsumeQueue);

            _flushTimer = new Timer(_ => Flush(), null, flushInterval, flushInterval);
        }

        public void Report(Span span)
        {
            bool added = false;
            try
            {
                // It's better to drop spans, than to block here
                added = _commandQueue.TryAdd(new AppendCommand(this, span));
            }
            catch (InvalidOperationException)
            {
                // The queue has been marked as Completed -> no-op.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{nameof(Report)}() failed");
            }

            if (!added)
            {
                _metrics.ReporterDropped.Inc(1);
            }
        }

        private void ConsumeQueue()
        {
            while (!_commandQueue.IsCompleted)
            {
                try
                {
                    // This blocks until a span is available.
                    ICommand command = _commandQueue.Take();

                    try
                    {
                        command.Execute();
                    }
                    catch (SenderException ex)
                    {
                        _metrics.ReporterFailure.Inc(ex.DroppedSpanCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "QueueProcessor error");
                    // Do nothing, and try again on next span.
                }
            }
        }

        public void Dispose()
        {
            try
            {
                // Note: Java creates a CloseCommand but we have CompleteAdding() in C# so we don't need the command.
                _commandQueue.CompleteAdding();

                // Give sender some time to process any queued spans.
                _queueProcessor.Wait(10000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispose interrupted");
            }
            finally
            {
                _flushTimer.Dispose();
                try
                {
                    int n = _sender.Close();
                    _metrics.ReporterSuccess.Inc(n);
                }
                catch (SenderException ex)
                {
                    _metrics.ReporterFailure.Inc(ex.DroppedSpanCount);
                }
            }
        }

        internal void Flush()
        {
            // to reduce the number of updateGauge stats, we only emit queue length on flush
            _metrics.ReporterQueueLength.Update(_commandQueue.Count);

            try
            {
                // We can safely drop FlushCommand when the queue is full - sender should take care of flushing
                // in such case
                _commandQueue.TryAdd(new FlushCommand(this));
            }
            catch (InvalidOperationException)
            {
                // The queue has been marked as Completed -> no-op.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{nameof(Flush)}() failed");
            }
        }

        /*
         * The code below implements the command pattern. This pattern is useful for
         * situations where multiple threads would need to synchronize on a resource,
         * but are fine with executing sequentially. The advantage is simplified code where
         * tasks are put onto a blocking queue and processed sequentially by another thread.
         */
        public interface ICommand
        {
            void Execute();
        }

        class AppendCommand : ICommand
        {
            private readonly RemoteReporter _reporter;
            private readonly Span _span;

            public AppendCommand(RemoteReporter reporter, Span span)
            {
                _reporter = reporter;
                _span = span;
            }

            public void Execute()
            {
                _reporter._sender.Append(_span);
            }
        }

        class FlushCommand : ICommand
        {
            private readonly RemoteReporter _reporter;

            public FlushCommand(RemoteReporter reporter)
            {
                _reporter = reporter;
            }

            public void Execute()
            {
                int n = _reporter._sender.Flush();
                _reporter._metrics.ReporterSuccess.Inc(n);
            }
        }

        public sealed class Builder
        {
            private ISender _sender;
            private IMetrics _metrics;
            private ILoggerFactory _loggerFactory;
            private TimeSpan _flushInterval = DefaultFlushInterval;
            private int _maxQueueSize = DefaultMaxQueueSize;

            public Builder WithFlushInterval(TimeSpan flushInterval)
            {
                _flushInterval = flushInterval;
                return this;
            }

            public Builder WithMaxQueueSize(int maxQueueSize)
            {
                _maxQueueSize = maxQueueSize;
                return this;
            }

            public Builder WithMetrics(IMetrics metrics)
            {
                _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
                return this;
            }

            public Builder WithSender(ISender sender)
            {
                _sender = sender ?? throw new ArgumentNullException(nameof(sender));
                return this;
            }

            public Builder WithLoggerFactory(ILoggerFactory loggerFactory)
            {
                _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
                return this;
            }

            public RemoteReporter Build()
            {
                if (_loggerFactory == null)
                {
                    _loggerFactory = NullLoggerFactory.Instance;
                }
                if (_sender == null)
                {
                    _sender = new UdpSender();
                }
                if (_metrics == null)
                {
                    _metrics = new MetricsImpl(NoopMetricsFactory.Instance);
                }
                return new RemoteReporter(_sender, _flushInterval, _maxQueueSize, _metrics, _loggerFactory);
            }
        }
    }
}