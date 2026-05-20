using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Middleware;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using FeedConstants = AvantiPoint.Packages.Core.FeedConstants;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Feed.Platform.Extensions;

public static class FeedServiceCollectionExtensions
{
    public static IServiceCollection AddAvantiPointFeedPlatform(
        this IServiceCollection services,
        IConfiguration configuration = null)
    {
        services.AddOptions<FeedOptions>();
        if (configuration is not null)
        {
            services.Configure<FeedOptions>(configuration.GetSection("Feed"));
            SyncLegacyFeedOptions(services, configuration);
        }

        services.TryAddScoped<ISurfaceContextAccessor, SurfaceContextAccessor>();
        services.TryAddSingleton<IPublicBaseUrlProvider, PublicBaseUrlProvider>();
        services.TryAddScoped<IStorageBackendFactory, StorageBackendFactory>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFeedProtocolAuthenticationAdapter, NuGetFeedAuthenticationAdapter>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFeedProtocolAuthenticationAdapter, NpmFeedAuthenticationAdapter>());
        services.TryAddScoped<IFeedAuthenticationService, CompositeFeedAuthenticationService>();

        services.TryAddSingleton(CreateDefaultFeedRegistry);

        return services;
    }

    public static FeedBuilder AddAvantiPointFeed(
        this WebApplicationBuilder builder,
        IConfigurationSection feedSection = null)
    {
        builder.Services.AddAvantiPointFeedPlatform(builder.Configuration);

        if (feedSection is not null)
        {
            builder.Services.Configure<FeedOptions>(feedSection);
        }

        var feedOptions = feedSection?.Get<FeedOptions>() ?? new FeedOptions();
        var feedId = string.IsNullOrWhiteSpace(feedOptions.Name) ? "default" : feedOptions.Name;
        var storagePrefix = feedOptions.Storage?.Prefix ?? $"feeds/{feedId}/";

        var registry = new FeedRegistry(new FeedContext(feedId, feedOptions.Name ?? feedId, storagePrefix));
        builder.Services.RemoveAll<IFeedRegistry>();
        builder.Services.AddSingleton<IFeedRegistry>(registry);

        return new FeedBuilder(builder.Services, registry);
    }

    /// <summary>
    /// Registers feed surface routing and OCI path rewriting. Call before <c>UseRouting()</c>.
    /// </summary>
    public static IApplicationBuilder UseAvantiPointFeedPlatform(this IApplicationBuilder app)
    {
        return app.UseMiddleware<FeedRouterMiddleware>();
    }

    private static IFeedRegistry CreateDefaultFeedRegistry(IServiceProvider sp)
    {
        var configuration = sp.GetService<IConfiguration>();
        var feedSection = configuration?.GetSection("Feed");
        var feedOptions = feedSection?.Exists() == true
            ? feedSection.Get<FeedOptions>() ?? new FeedOptions()
            : new FeedOptions();

        var feedId = string.IsNullOrWhiteSpace(feedOptions.Name) ? FeedConstants.DefaultFeedId : feedOptions.Name;
        var storagePrefix = feedOptions.Storage?.Prefix ?? string.Empty;
        return new FeedRegistry(new FeedContext(feedId, feedOptions.Name ?? feedId, storagePrefix));
    }

    private static void SyncLegacyFeedOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.PostConfigure<FeedOptions>(feed =>
        {
            if (!string.IsNullOrEmpty(feed.Authentication.ApiKey))
            {
                return;
            }

            var legacyKey = configuration["ApiKey"] ?? configuration["PackageFeed:ApiKey"];
            if (!string.IsNullOrEmpty(legacyKey))
            {
                feed.Authentication.ApiKey = legacyKey;
            }
        });

        services.PostConfigure<PackageFeedOptions>(legacy =>
        {
            var feed = configuration.GetSection("Feed").Get<FeedOptions>();
            if (feed?.Authentication?.ApiKey is { } key && string.IsNullOrEmpty(legacy.ApiKey))
            {
                legacy.ApiKey = key;
            }
        });
    }
}
