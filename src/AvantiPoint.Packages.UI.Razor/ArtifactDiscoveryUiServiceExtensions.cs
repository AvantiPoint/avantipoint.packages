using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI;

public static class ArtifactDiscoveryUiServiceExtensions
{
    public static IServiceCollection AddArtifactDiscoveryUi(this IServiceCollection services)
    {
        services.AddScoped<IArtifactDiscoveryService, ArtifactDiscoveryService>();
        return services;
    }
}
