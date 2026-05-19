using AvantiPoint.Feed.Platform.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Feed.Platform;

public sealed class FeedBuilder
{
    private readonly IFeedRegistry _registry;

    internal FeedBuilder(IServiceCollection services, IFeedRegistry registry)
    {
        Services = services;
        _registry = registry;
    }

    public IServiceCollection Services { get; }

    public IFeedRegistry Registry => _registry;

    public FeedBuilder UseNuGet(string surfaceId = "nuget")
    {
        _registry.Register(new SurfaceRegistration(
            surfaceId,
            FeedProtocol.NuGet,
            OciSegment: null,
            RoutePrefix: string.Empty,
            OptionsSectionKey: "Feed:NuGet"));

        return this;
    }

    public FeedBuilder UseNpm(string routePrefix = "/npm", string surfaceId = "npm")
    {
        var prefix = NormalizeRoutePrefix(routePrefix);
        _registry.Register(new SurfaceRegistration(
            surfaceId,
            FeedProtocol.Npm,
            OciSegment: null,
            RoutePrefix: prefix,
            OptionsSectionKey: "Feed:Npm"));

        Services.AddOptions<NpmFeedOptions>()
            .BindConfiguration("Feed:Npm");

        return this;
    }

    public FeedBuilder UseOciDefault(string surfaceId = "oci-default")
    {
        _registry.Register(new SurfaceRegistration(
            surfaceId,
            FeedProtocol.Oci,
            OciSegment: null,
            RoutePrefix: string.Empty,
            OptionsSectionKey: "Feed:Oci:Default"));

        return this;
    }

    public FeedBuilder UseOci(
        string segment,
        string surfaceId = null,
        Action<OciSurfaceOptionsBuilder> configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segment);
        surfaceId ??= $"oci-{segment}";

        _registry.Register(new SurfaceRegistration(
            surfaceId,
            FeedProtocol.Oci,
            OciSegment: segment,
            RoutePrefix: $"/{segment.Trim('/')}",
            OptionsSectionKey: $"Feed:Oci:{ToOptionsKey(segment)}"));

        configure?.Invoke(new OciSurfaceOptionsBuilder(Services, segment));
        return this;
    }

    private static string ToOptionsKey(string segment) =>
        char.ToUpperInvariant(segment[0]) + segment[1..];

    private static string NormalizeRoutePrefix(string routePrefix)
    {
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            return "/npm";
        }

        var prefix = routePrefix.Trim();
        return prefix.StartsWith('/') ? prefix.TrimEnd('/') : "/" + prefix.TrimEnd('/');
    }
}

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
