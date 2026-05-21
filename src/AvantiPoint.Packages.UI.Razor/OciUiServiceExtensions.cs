using AvantiPoint.Packages.Registry.Oci;
using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI;

public static class OciUiServiceExtensions
{
    public static IServiceCollection AddOciRepositoryBrowseUi(this IServiceCollection services)
    {
        services.AddOciRegistry();
        services.AddScoped<IOciRepositoryBrowseService, OciRepositoryBrowseService>();
        return services;
    }
}
