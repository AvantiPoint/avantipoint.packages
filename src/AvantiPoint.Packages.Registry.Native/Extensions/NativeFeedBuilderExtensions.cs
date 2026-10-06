using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Registry.Native.Maven;
using AvantiPoint.Packages.Registry.Native.Pub;
using AvantiPoint.Packages.Registry.Native.Storage;
using AvantiPoint.Packages.Registry.Native.Swift;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Registry.Native.Extensions;

public static class NativeFeedBuilderExtensions
{
    public static FeedBuilder UseNativeRegistriesIfEnabled(this FeedBuilder feed, IConfigurationSection section)
    {
        foreach (var protocol in new[] { FeedProtocol.Maven, FeedProtocol.Swift, FeedProtocol.Pub })
            if (section.GetSection(protocol.ToString()).GetValue<bool>("Enabled")) feed.UseNativeRegistry(protocol);
        return feed;
    }

    public static FeedBuilder UseNativeRegistry(this FeedBuilder feed, FeedProtocol protocol)
    {
        if (protocol is not (FeedProtocol.Maven or FeedProtocol.Swift or FeedProtocol.Pub))
            throw new ArgumentOutOfRangeException(nameof(protocol));
        var name = protocol.ToString();
        var prefix = "/" + name.ToLowerInvariant();
        if (feed.Registry.Surfaces.Any(s => s.RoutePrefix.Equals(prefix, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"The route prefix '{prefix}' is already in use.");
        feed.Registry.Register(new(name.ToLowerInvariant(), protocol, null, prefix, "Feed:" + name));
        feed.Services.TryAddScoped<NativeArtifactStore>();
        feed.Services.AddOptions<NativeRegistryOptions>(name).BindConfiguration("Feed:" + name)
            .Validate(o => o.MaxArtifactBytes is > 0 and <= 4L * 1024 * 1024 * 1024
                && o.MaxExpandedArchiveBytes >= o.MaxArtifactBytes && o.MaxArchiveEntries is > 0 and <= 100000,
                "Native artifact and archive limits must be positive and bounded.")
            .ValidateOnStart();
        // Stable same-origin URLs keep pub credentials scoped correctly and avoid
        // trusting request or forwarding headers when constructing upload links.
        feed.Services.AddOptions<FeedOptions>().Validate(o =>
            Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback))
            && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment),
            "Native registries require Feed:PublicBaseUrl with HTTPS (HTTP is allowed only for loopback development).")
            .ValidateOnStart();
        return feed;
    }

    public static WebApplication MapNativeFeeds(this WebApplication app, FeedBuilder feed)
    {
        foreach (var surface in feed.Registry.Surfaces)
            switch (surface.Protocol)
            {
                case FeedProtocol.Maven: app.MapMavenRegistry(surface.RoutePrefix); break;
                case FeedProtocol.Swift: app.MapSwiftBinaries(surface.RoutePrefix); break;
                case FeedProtocol.Pub: app.MapPubRegistry(surface.RoutePrefix); break;
            }
        return app;
    }
}
