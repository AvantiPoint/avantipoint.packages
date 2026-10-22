using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI;

public static class NpmUiServiceExtensions
{
    public static IServiceCollection AddNpmPackageBrowseUi(this IServiceCollection services)
    {
        services.AddScoped<INpmPackageBrowseService, NpmPackageBrowseService>();
        services.AddScoped<INpmPackageComparisonService, NpmPackageComparisonService>();
        return services;
    }
}
