using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Jaeger.Core.Util;

namespace Jaeger.Core.Samplers
{
    public sealed class SamplingStatus : ValueObject
    {
        public bool IsSampled { get; }
        public ReadOnlyDictionary<string, object> Tags { get; }

        public SamplingStatus(bool isSampled, ReadOnlyDictionary<string, object> tags)
        {
            IsSampled = isSampled;
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        }

        protected override IEnumerable<object> GetAtomicValues()
        {
            yield return IsSampled;
            yield return Tags;
        }
    }
}