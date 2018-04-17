using System.Collections.Generic;
using Jaeger.Core.Util;

namespace Jaeger.Core.Samplers.HTTP
{
    public class RateLimitingSamplingStrategy : ValueObject
    {
        public short MaxTracesPerSecond { get; }

        public RateLimitingSamplingStrategy(short maxTracesPerSecond)
        {
            MaxTracesPerSecond = maxTracesPerSecond;
        }

        protected override IEnumerable<object> GetAtomicValues()
        {
            yield return MaxTracesPerSecond;
        }
    }
}