using AvantiPoint.Feed.Platform.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace AvantiPoint.Feed.Platform;

public sealed class OciSurfaceOptionsBuilder
{
    internal OciSurfaceOptionsBuilder(IServiceCollection services, string segment)
    {
        Segment = segment;
        Services = services;
    }

    public string Segment { get; }

    public IServiceCollection Services { get; }
}
