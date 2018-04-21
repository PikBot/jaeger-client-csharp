using System;
using Jaeger.Core.Samplers;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class JaegerBuilderExtensions
    {
        public static IJaegerBuilder Configure(this IJaegerBuilder builder, Action<JaegerOptions> options)
        {
            builder.Services.Configure<JaegerOptions>(options);
            return builder;
        }

        public static IJaegerBuilder UseConfiguration(this IJaegerBuilder builder, IConfiguration config)
        {
            builder.Services.Configure<JaegerOptions>(config);
            return builder;
        }

        public static IJaegerBuilder UseSampler(this IJaegerBuilder builder, ISampler sampler)
        {
            builder.Services.AddSingleton(sampler);
            return builder;
        }

        public static IJaegerBuilder UseConstSampler(this IJaegerBuilder builder, bool sample = true)
        {
            builder.Services.AddSingleton<ISampler>(_ => new ConstSampler(sample));
            return builder;
        }
    }
}