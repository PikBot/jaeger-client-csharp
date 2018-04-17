using System.Collections.Generic;
using System.Threading;
using Jaeger.Core.Reporters.Protocols;
using Jaeger.Core.Senders;
using ThriftSpan = Jaeger.Thrift.Span;

namespace Jaeger.Core.Tests.Reporters
{
    /// <summary>
    /// Sender which stores spans in memory. Appending a new span is a blocking operation unless
    /// "permitted". By default <see cref="int.MaxValue"/> "appends" are permitted.
    /// </summary>
    public class InMemorySender : ISender
    {
        private readonly List<ThriftSpan> _appended;
        private readonly List<ThriftSpan> _flushed;
        private readonly List<ThriftSpan> _received;

        // By default, all Append actions are allowed.
        private ManualResetEventSlim _blocker = new ManualResetEventSlim(true);

        public volatile int FlushCallCount;

        public InMemorySender()
        {
            _appended = new List<ThriftSpan>();
            _flushed = new List<ThriftSpan>();
            _received = new List<ThriftSpan>();
        }

        public List<ThriftSpan> GetAppended()
        {
            return new List<ThriftSpan>(_appended);
        }

        public List<ThriftSpan> GetFlushed()
        {
            return new List<ThriftSpan>(_flushed);
        }

        public List<ThriftSpan> GetReceived()
        {
            return new List<ThriftSpan>(_received);
        }

        public int Append(Span span)
        {
            _blocker.Wait();

            ThriftSpan thriftSpan = JaegerThriftSpanConverter.ConvertSpan(span);
            _appended.Add(thriftSpan);
            _received.Add(thriftSpan);
            return 0;
        }

        public int Flush()
        {
            FlushCallCount++;

            int flushedSpans = _appended.Count;
            _flushed.AddRange(_appended);
            _appended.Clear();

            return flushedSpans;
        }

        public int Close()
        {
            return Flush();
        }

        public void BlockAppend()
        {
            _blocker.Reset();
        }

        public void AllowAppend()
        {
            _blocker.Set();
        }
    }
}