using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Npm.Extensions;

public static class NpmFeedBuilderExtensions
{
    public static FeedBuilder UseNpmRegistry(
        this FeedBuilder feed,
        string routePrefix = "/npm",
        string surfaceId = "npm")
    {
        feed.UseNpm(routePrefix, surfaceId);
        feed.Services.AddNpmRegistry();
        return feed;
    }

    public static WebApplication MapNpmFeed(this WebApplication app, FeedBuilder feed)
    {
        var npmSurface = feed.Registry.TryGetNpmSurface();
        if (npmSurface is null)
        {
            return app;
        }

        app.MapNpmRegistryRoutes(npmSurface.RoutePrefix);
        return app;
    }
}
