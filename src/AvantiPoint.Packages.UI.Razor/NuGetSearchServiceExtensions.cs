using System;
using System.Net.Http;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.UI;

/// <summary>
/// Extension methods for registering the NuGet search service.
/// </summary>
public static class NuGetSearchServiceExtensions
{
    /// <summary>
    /// Adds the NuGet search service to the DI container using the Protocol library for endpoint discovery.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for the search service options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNuGetSearchService(
        this IServiceCollection services,
        Action<NuGetSearchServiceOptions>? configure = null)
    {
        services.AddHttpClient();
        services.AddHttpContextAccessor();

        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddScoped<INuGetSearchService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
            var options = sp.GetRequiredService<IOptions<NuGetSearchServiceOptions>>().Value;

            var httpClient = httpClientFactory.CreateClient();
            var httpContext = httpContextAccessor.HttpContext;

            // Configure the HttpClient with user-specific authentication if callback is provided
            if (options.ConfigureHttpClient is not null && httpContext is not null)
            {
                options.ConfigureHttpClient(httpContext, httpClient);
            }

            // Resolve the service index URL (relative or absolute)
            var serviceIndexUrl = options.ServiceIndexUrl;
            if (httpContext is not null && !Uri.IsWellFormedUriString(serviceIndexUrl, UriKind.Absolute))
            {
                var request = httpContext.Request;
                var baseUri = new Uri($"{request.Scheme}://{request.Host}{request.PathBase}");
                serviceIndexUrl = new Uri(baseUri, serviceIndexUrl).ToString();
            }

            // Use the Protocol library to auto-discover the search endpoint from the service index
            return new ProtocolBasedSearchService(httpClient, serviceIndexUrl);
        });

        return services;
    }
}
