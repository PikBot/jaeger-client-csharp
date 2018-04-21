using System;

namespace Microsoft.Extensions.DependencyInjection
{
    internal class JaegerBuilder : IJaegerBuilder
    {
        public IServiceCollection Services { get; }

        public JaegerBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }
    }
}
