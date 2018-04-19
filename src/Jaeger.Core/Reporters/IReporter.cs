using System;

namespace Jaeger.Core.Reporters
{
    /// <summary>
    /// <see cref="IReporter"/> is the interface <see cref="Tracer"/> uses to report finished span to something that
    /// collects those spans. Default implementation is <see cref="RemoteReporter"/> that sends spans out of process.
    /// </summary>
    public interface IReporter : IDisposable
    {
        void Report(Span span);
    }
}
