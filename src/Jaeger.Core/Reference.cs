using System.Collections.Generic;
using Jaeger.Core.Util;
using OpenTracing;

namespace Jaeger.Core
{
    public sealed class Reference : ValueObject
    {
        public string Type { get; }
        public SpanContext Context { get; }

        public Reference(SpanContext context, string type)
        {
            Type = type;
            Context = context;
        }

        protected override IEnumerable<object> GetAtomicValues()
        {
            yield return Type;
            yield return Context;
        }
    }
}