using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jaeger.Core.Exceptions;
using Jaeger.Core.Reporters.Protocols;
using Jaeger.Thrift.Senders;
using ThriftProcess = Jaeger.Thrift.Process;
using ThriftSpan = Jaeger.Thrift.Span;

namespace Jaeger.Core.Senders
{
    /// <summary>
    /// JaegerThriftTransport is the base class for transporting spans from C# into Jaeger.
    /// It provides the basic implementation of serializing spans but leaves the buffering
    /// and sending of the serialized spans to concrete implementations.
    /// </summary>
    public abstract class ThriftSender : ThriftSenderBase, ISender
    {
        private ThriftProcess _process;
        private int _processBytesSize;
        private readonly List<ThriftSpan> _spanBuffer = new List<ThriftSpan>();
        private int _byteBufferSize;

        protected ThriftSender(ProtocolType protocolType, int maxPacketSize)
            : base(protocolType, maxPacketSize)
        {
        }

        public int Append(Span span)
        {
            if (_process == null)
            {
                _process = new ThriftProcess(span.Tracer.ServiceName);
                _process.Tags = JaegerThriftSpanConverter.BuildTags(span.Tracer.Tags);
                _processBytesSize = CalculateProcessSize(_process);
                _byteBufferSize += _processBytesSize;
            }

            ThriftSpan thriftSpan = JaegerThriftSpanConverter.ConvertSpan(span);
            int spanSize = CalculateSpanSize(thriftSpan);
            if (spanSize > MaxSpanBytes)
            {
                throw new SenderException($"ThriftSender received a span that was too large, size = {spanSize}, max = {MaxSpanBytes}", null, 1);
            }

            _byteBufferSize += spanSize;
            if (_byteBufferSize <= MaxSpanBytes)
            {
                _spanBuffer.Add(thriftSpan);
                if (_byteBufferSize < MaxSpanBytes)
                {
                    return 0;
                }
                return Flush();
            }

            int n;
            try
            {
                n = Flush();
            }
            catch (SenderException ex)
            {
                // +1 for the span not submitted in the buffer above
                throw new SenderException(ex.Message, ex, ex.DroppedSpanCount + 1);
            }

            _spanBuffer.Add(thriftSpan);
            _byteBufferSize = _processBytesSize + spanSize;
            return n;
        }

        protected int CalculateProcessSize(ThriftProcess proc)
        {
            try
            {
                return GetSize(proc);
            }
            catch (Exception ex)
            {
                throw new SenderException("ThriftSender failed writing Process to memory buffer.", ex, 1);
            }
        }

        protected int CalculateSpanSize(ThriftSpan span)
        {
            try
            {
                return GetSize(span);
            }
            catch (Exception ex)
            {
                throw new SenderException("ThriftSender failed writing Span to memory buffer.", ex, 1);
            }
        }

        protected abstract Task SendAsync(ThriftProcess process, List<ThriftSpan> spans);

        public int Flush()
        {
            if (_spanBuffer.Count == 0)
            {
                return 0;
            }

            int n = _spanBuffer.Count;
            try
            {
                SendAsync(_process, _spanBuffer).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (SenderException ex)
            {
                throw new SenderException("Failed to flush spans.", ex, n);
            }
            finally
            {
                _spanBuffer.Clear();
                _byteBufferSize = _processBytesSize;
            }
            return n;
        }

        public virtual int Close()
        {
            return Flush();
        }
    }
}
