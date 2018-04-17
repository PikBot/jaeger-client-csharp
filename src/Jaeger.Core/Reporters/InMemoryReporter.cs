using System.Collections.Generic;

namespace Jaeger.Core.Reporters
{
    public class InMemoryReporter : IReporter
    {
        private readonly object _lock = new object();
        private readonly List<Span> _spans = new List<Span>();

        public void Report(Span span)
        {
            lock (_lock)
            {
                _spans.Add(span);
            }
        }

        public List<Span> GetSpans()
        {
            lock (_lock)
            {
                return _spans;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _spans.Clear();
            }
        }

        public void Dispose()
        {
        }
    }
}
