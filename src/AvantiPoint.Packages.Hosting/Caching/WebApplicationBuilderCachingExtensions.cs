using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Hosting.Caching;

internal static class WebApplicationBuilderCachingExtensions
{
    public static IServiceCollection AddNuGetCaching(this IServiceCollection services, IConfiguration configuration)
    {
        // bind settings
        services.Configure<CacheSettings>(configuration.GetSection("Caching"));
        var settings = configuration.GetSection("Caching")
                                          .Get<CacheSettings>()
                       ?? new CacheSettings();

        // register only what you need
        if (settings.Type == CacheType.None)
            return services;

        services.AddMemoryCache();
        if (!string.IsNullOrEmpty(settings.RedisConnection))
        {
            services.AddStackExchangeRedisCache(opts =>
            {
                opts.Configuration = settings.RedisConnection!;
            });
        }

        services.AddHybridCache();

        services.AddOutputCache(o =>
        {
            o.AddPolicy(CacheSettings.CachePolicyName, policy =>
            {
                policy.Expire(settings.DefaultTTL)
                      .SetVaryByQuery("packageId", "version", "includePrerelease", "includeUnlisted")
                      .SetVaryByHeader("Accept-Encoding");
            });
        });

        return services;
    }

    public static WebApplicationBuilder AddNuGetCaching(this WebApplicationBuilder builder)
    {
        AddNuGetCaching(builder.Services, builder.Configuration);
        return builder;
    }
}
