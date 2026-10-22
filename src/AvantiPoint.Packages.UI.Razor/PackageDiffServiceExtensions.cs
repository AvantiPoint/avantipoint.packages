using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.UI;

public static class PackageDiffServiceExtensions
{
    public static IServiceCollection AddPackageDiffService(this IServiceCollection services)
    {
        services.TryAddSingleton<ISourceLinkContentFetcher, SourceLinkContentFetcher>();
        services.AddScoped<IPackageDiffService, PackageDiffService>();
        return services;
    }
}
