using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
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

    public static FeedBuilder UseOciRegistry(
        this FeedBuilder feed,
        string segment,
        bool allowV2EmbeddedSegmentRouting = false)
    {
        feed.UseOci(segment, allowV2EmbeddedSegmentRouting: allowV2EmbeddedSegmentRouting);
        feed.Services.AddOciRegistry();
        return feed;
    }

    public static FeedBuilder UseConfiguredOciSurfaces(this FeedBuilder feed, IConfigurationSection ociSection)
    {
        ArgumentNullException.ThrowIfNull(ociSection);

        if (ociSection.GetSection("Default").GetValue<bool>("Enabled"))
        {
            feed.UseOciRegistry();
        }

        foreach (var child in ociSection.GetChildren())
        {
            if (child.Key.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (child.GetValue<bool>("Enabled"))
            {
                feed.UseOciRegistry(ToOciSegment(child.Key));
            }
        }

        return feed;
    }

    private static string ToOciSegment(string optionsKey) =>
        char.ToLowerInvariant(optionsKey[0]) + optionsKey[1..];

    public static WebApplication MapOciFeed(this WebApplication app, FeedBuilder feed)
    {
        if (feed.Registry.Surfaces.Any(s => s.Protocol == FeedProtocol.Oci))
        {
            app.MapOciTokenRoutes(feed.Registry);
            app.MapOciRegistryRoutes();
        }

        return app;
    }
}
