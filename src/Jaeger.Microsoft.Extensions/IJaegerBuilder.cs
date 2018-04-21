namespace Microsoft.Extensions.DependencyInjection
{
    public interface IJaegerBuilder
    {
        IServiceCollection Services { get; }
    }
}
