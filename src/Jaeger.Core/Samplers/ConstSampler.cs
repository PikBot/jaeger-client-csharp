using System.Collections.Generic;
using System.Collections.ObjectModel;
using Jaeger.Core.Util;

namespace Jaeger.Core.Samplers
{
    public class ConstSampler : ValueObject, ISampler
    {
        public const string Type = "const";

        private readonly bool _decision;
        private readonly IReadOnlyDictionary<string, object> _tags;

        public ConstSampler(bool sample)
        {
            _decision = sample;
            _tags = new ReadOnlyDictionary<string, object>(new Dictionary<string, object> {
                { Constants.SamplerTypeTagKey, Type },
                { Constants.SamplerParamTagKey, sample }
            });
        }

        public SamplingStatus Sample(string operation, TraceId id)
        {
            return new SamplingStatus(_decision, _tags);
        }

        public void Dispose()
        {
            // nothing to do
        }

        public override string ToString()
        {
            return $"{nameof(ConstSampler)}({_decision})";
        }

        protected override IEnumerable<object> GetAtomicValues()
        {
            yield return _decision;
        }
    }
}
