using System;

namespace Jaeger.Core.Samplers
{
    /// <summary>
    /// <see cref="ISampler"/> is responsible for deciding if a new trace should be sampled and captured for storage.
    /// </summary>
    public interface ISampler : IDisposable
    {
        /// <summary>
        /// Returns whether or not the new trace should be sampled.
        /// </summary>
        /// <param name="operation">The operation name set on the span.</param>
        /// <param name="id">The traceId on the span.</param>
        SamplingStatus Sample(string operation, TraceId id);
    }
}
