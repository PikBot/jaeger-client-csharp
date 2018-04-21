using System;
using Jaeger.Core.Baggage;
using Jaeger.Core.Metrics;
using Jaeger.Core.Reporters;
using Jaeger.Core.Samplers;
using Jaeger.Core.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTracing;
using OpenTracing.Util;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddJaeger(this IServiceCollection services, Action<IJaegerBuilder> configure = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<ITracer>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<JaegerOptions>>().Value;

                // The builder will use its default if a given service is null.
                var tracerBuilder = new Jaeger.Core.Tracer.Builder(options.ServiceName)
                    .WithBaggageRestrictionManager(serviceProvider.GetService<IBaggageRestrictionManager>())
                    .WithClock(serviceProvider.GetService<IClock>())
                    .WithLoggerFactory(serviceProvider.GetService<ILoggerFactory>())
                    .WithMetricsFactory(serviceProvider.GetService<IMetricsFactory>())
                    .WithReporter(serviceProvider.GetService<IReporter>())
                    .WithSampler(serviceProvider.GetService<ISampler>())
                    .WithScopeManager(serviceProvider.GetService<IScopeManager>());

                if (options.ExpandExceptionLogs)
                {
                    tracerBuilder.WithExpandExceptionLogs();
                }
                if (options.ZipkinSharedRpcSpan)
                {
                    tracerBuilder.WithZipkinSharedRpcSpan();
                }

                foreach (var tag in options.Tags)
                {
                    if (tag.Value is bool boolValue)
                    {
                        tracerBuilder.WithTag(tag.Key, boolValue);
                    }
                    else if (tag.Value is double doubleValue)
                    {
                        tracerBuilder.WithTag(tag.Key, doubleValue);
                    }
                    else if (tag.Value is int intValue)
                    {
                        tracerBuilder.WithTag(tag.Key, intValue);
                    }
                    else if (tag.Value is string stringValue)
                    {
                        tracerBuilder.WithTag(tag.Key, stringValue);
                    }
                    else
                    {
                        tracerBuilder.WithTag(tag.Key, tag.Value.ToString());
                    }
                }

                ITracer tracer = tracerBuilder.Build();

                // Allows code that can't use DI to also access the tracer.
                GlobalTracer.Register(tracer);

                return tracer;
            });

            return services;
        }
    }
}
