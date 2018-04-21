using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public class JaegerOptions
    {
        public string ServiceName { get; set; }

        public bool ExpandExceptionLogs { get; set; }

        public bool ZipkinSharedRpcSpan { get; set; }

        public Dictionary<string, object> Tags { get; } = new Dictionary<string, object>();

        public JaegerOptions()
        {
            // Defaults
            ServiceName = Assembly.GetEntryAssembly().GetName().Name;
        }
    }
}
