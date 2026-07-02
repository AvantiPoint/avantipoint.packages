using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Services.Upstreams;
using AvantiPoint.Packages.Registry.Npm;
using AvantiPoint.Packages.Registry.Oci;

namespace AvantiPoint.Packages.Host.Extensions;

public static class HostUpstreamRegistryExtensions
{
    /// <summary>
    /// Registers database-backed upstream registry providers for npm and OCI so mirror
    /// sources are managed at runtime from the admin UI (with static configuration as
    /// seed/fallback). Must run before the npm/OCI registries register their
    /// configuration-only defaults.
    /// </summary>
    public static IServiceCollection AddHostDatabaseUpstreamProviders(this IServiceCollection services)
    {
        services.AddScoped<INpmUpstreamRegistryProvider, DatabaseNpmUpstreamRegistryProvider>();

        services.AddScoped<ConfigurationOciUpstreamRegistryProvider>();
        services.AddScoped<IOciUpstreamRegistryProvider>(sp => new DatabaseOciUpstreamRegistryProvider(
            sp.GetRequiredService<IPackageSourceService>(),
            sp.GetRequiredService<ISecretProtector>(),
            sp.GetRequiredService<ConfigurationOciUpstreamRegistryProvider>()));

        return services;
    }
}
