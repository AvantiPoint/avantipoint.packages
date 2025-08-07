using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Hosting.Caching;

internal static class RouteHandlerBuilderExtensions
{
    
    public static RouteHandlerBuilder UseNugetCaching(
        this RouteHandlerBuilder builder)
    {
        builder.CacheOutput(CacheSettings.CachePolicyName);

        return builder;
    }
}