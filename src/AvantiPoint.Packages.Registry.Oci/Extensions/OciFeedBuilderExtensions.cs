using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci.Extensions;

public static class OciFeedBuilderExtensions
{
    public static FeedBuilder UseOciRegistry(this FeedBuilder feed)
    {
        feed.UseOciDefault();
        feed.Services.AddOciRegistry();
        return feed;
    }

    public static FeedBuilder UseOciRegistry(this FeedBuilder feed, string segment)
    {
        feed.UseOci(segment);
        feed.Services.AddOciRegistry();
        return feed;
    }

    public static WebApplication MapOciFeed(this WebApplication app, FeedBuilder feed)
    {
        if (feed.Registry.Surfaces.Any(s => s.Protocol == FeedProtocol.Oci))
        {
            app.MapOciRegistryRoutes();
        }

        return app;
    }
}
