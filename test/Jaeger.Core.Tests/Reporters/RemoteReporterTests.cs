using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Jaeger.Core.Metrics;
using Jaeger.Core.Reporters;
using Jaeger.Core.Samplers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ThriftSpan = Jaeger.Thrift.Span;

namespace Jaeger.Core.Tests.Reporters
{
    public class RemoteReporterTests
    {
        private const int MaxQueueSize = 500;
        private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(1);

        private IReporter _reporter;
        private Tracer _tracer;
        private InMemorySender _sender;
        private readonly IMetrics _metrics;
        private readonly InMemoryMetricsFactory _metricsFactory;

        public RemoteReporterTests()
        {
            _metricsFactory = new InMemoryMetricsFactory();
            _metrics = new MetricsImpl(_metricsFactory);

            _sender = new InMemorySender();
            _reporter = new RemoteReporter.Builder()
                .WithSender(_sender)
                .WithFlushInterval(_flushInterval)
                .WithMaxQueueSize(MaxQueueSize)
                .WithMetrics(_metrics)
                .Build();
            _tracer = new Tracer.Builder("test-remote-reporter")
                .WithReporter(_reporter)
                .WithSampler(new ConstSampler(true))
                .WithMetrics(_metrics)
                .Build();
        }

        [Fact]
        public void TestRemoteReporterReport()
        {
            Span span = (Span)_tracer.BuildSpan("raza").Start();
            _reporter.Report(span);

            // do sleep until automatic flush happens on 'reporter'
            // added 20ms on top of 'flushInterval' to avoid corner cases
            double timeout = _flushInterval.Add(TimeSpan.FromMilliseconds(20)).TotalMilliseconds;
            Stopwatch timer = Stopwatch.StartNew();
            while (_sender.GetReceived().Count == 0 && timer.ElapsedMilliseconds < timeout)
            {
                Thread.Sleep(1);
            }

            List<ThriftSpan> received = _sender.GetReceived();

            Assert.Single(received);
        }

        [Fact]
        public void TestRemoteReporterFlushesOnDispose()
        {
            int numberOfSpans = 100;
            for (int i = 0; i < numberOfSpans; i++)
            {
                Span span = (Span)_tracer.BuildSpan("raza").Start();
                _reporter.Report(span);
            }
            _reporter.Dispose();

            Assert.Empty(_sender.GetAppended());
            Assert.Equal(numberOfSpans, _sender.GetFlushed().Count);

            Assert.Equal(100, _metricsFactory.GetCounter("jaeger:started_spans", "sampled=y"));
            Assert.Equal(100, _metricsFactory.GetCounter("jaeger:reporter_spans", "result=ok"));
            Assert.Equal(100, _metricsFactory.GetCounter("jaeger:traces", "sampled=y,state=started"));
        }

        // Starts a number of threads. Each can fill the queue on its own, so they will exceed its
        // capacity many times over
        [Fact]
        public void TestReportDoesntThrowWhenQueueFull()
        {
            ConcurrentBag<Exception> exceptions = new ConcurrentBag<Exception>();

            int threadsCount = 10;
            Barrier barrier = new Barrier(threadsCount);
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < threadsCount; i++)
            {
                Task t = CreateSpanReportingTask(exceptions, barrier);
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            Assert.Empty(exceptions);
        }

        private Task CreateSpanReportingTask(ConcurrentBag<Exception> exceptions, Barrier barrier)
        {
            return Task.Factory.StartNew(() =>
            {
                for (int x = 0; x < MaxQueueSize; x++)
                {
                    try
                    {
                        barrier.SignalAndWait();
                        _reporter.Report(NewSpan());
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }

        [Fact]
        public void TestAppendWhenQueueFull()
        {
            // change sender to blocking mode
            _sender.BlockAppend();

            for (int i = 0; i < MaxQueueSize; i++)
            {
                _reporter.Report(NewSpan());
            }

            // When: at this point the queue is full or there is one slot empty (if the worker thread has
            // already picked up some command). We add two spans to make sure that we overfill the queue
            _reporter.Report(NewSpan());
            _reporter.Report(NewSpan());

            // Then: one or both spans should be dropped
            long droppedCount = _metricsFactory.GetCounter("jaeger:reporter_spans", "result=dropped");
            Assert.InRange(droppedCount, 1, 2);
        }

        [Fact]
        public void TestCloseWhenQueueFull()
        {
            _reporter = new RemoteReporter(_sender, TimeSpan.FromHours(1), MaxQueueSize, _metrics, NullLoggerFactory.Instance);
            _tracer = new Tracer.Builder("test-remote-reporter")
                .WithReporter(_reporter)
                .WithSampler(new ConstSampler(true))
                .WithMetrics(_metrics)
                .Build();

            // change sender to blocking mode
            _sender.BlockAppend();

            // fill the queue
            for (int i = 0; i < MaxQueueSize + 10; i++)
            {
                _reporter.Report(NewSpan());
            }

            _reporter.Dispose();

            // expect no exception thrown
        }

        [Fact]
        public void TestFlushWhenQueueFull()
        {
            // change sender to blocking mode
            _sender.BlockAppend();

            // fill the queue
            for (int i = 0; i < MaxQueueSize + 10; i++)
            {
                _reporter.Report(NewSpan());
            }

            ((RemoteReporter)_reporter).Flush();

            // expect no exception thrown
        }

        [Fact]
        public void TestFlushUpdatesQueueLength()
        {
            TimeSpan neverFlushInterval = TimeSpan.FromHours(1);
            _reporter = new RemoteReporter(_sender, neverFlushInterval, MaxQueueSize, _metrics, NullLoggerFactory.Instance);
            _tracer = new Tracer.Builder("test-remote-reporter")
                .WithReporter(_reporter)
                .WithSampler(new ConstSampler(true))
                .WithMetrics(_metrics)
                .Build();

            // change sender to blocking mode
            _sender.BlockAppend();

            for (int i = 0; i < 3; i++)
            {
                _reporter.Report(NewSpan());
            }

            Assert.Equal(0, _metricsFactory.GetGauge("jaeger:reporter_queue_length", ""));

            RemoteReporter remoteReporter = (RemoteReporter)_reporter;
            remoteReporter.Flush();

            Assert.True(_metricsFactory.GetGauge("jaeger:reporter_queue_length", "") > 0);
        }

        [Fact]
        public void TestFlushIsCalledOnSender()
        {
            _tracer.BuildSpan("mySpan").Start().Finish();

            // do sleep until automatic flush happens on 'reporter'
            double timeout = _flushInterval.Add(TimeSpan.FromMilliseconds(20)).TotalMilliseconds;
            Stopwatch timer = Stopwatch.StartNew();
            while (_sender.FlushCallCount == 0 && timer.ElapsedMilliseconds < timeout)
            {
                Thread.Sleep(1);
            }

            Assert.True(_sender.FlushCallCount > 0);
        }

        private Span NewSpan()
        {
            return (Span)_tracer.BuildSpan("x").Start();
        }
    }
}
