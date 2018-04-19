namespace Jaeger.Core.Reporters
{
    public class NoopReporter : IReporter
    {
        public void Report(Span span)
        {
        }

        public void Dispose()
        {
        }
    }
}
