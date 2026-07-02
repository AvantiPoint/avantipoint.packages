using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Host.Admin.Services.Upstreams;

/// <summary>
/// Resolves upstream OCI registries from the database (<see cref="PackageSource"/> rows with
/// <see cref="PackageSourceProtocol.Oci"/>) so they can be managed at runtime from the admin UI.
/// Falls back to static configuration when no database sources are defined.
/// </summary>
public sealed class DatabaseOciUpstreamRegistryProvider(
    IPackageSourceService packageSourceService,
    ISecretProtector secretProtector,
    IOciUpstreamRegistryProvider configurationProvider) : IOciUpstreamRegistryProvider
{
    public async ValueTask<IReadOnlyList<OciUpstreamRegistryOptions>> GetRegistriesAsync(
        SurfaceContext surface,
        CancellationToken cancellationToken = default)
    {
        var sources = await packageSourceService.GetEnabledUpstreamSourcesAsync(
            PackageSourceProtocol.Oci,
            cancellationToken);

        if (sources.Count == 0)
        {
            // Seed/fallback: static configuration keeps working until sources are added.
            return await configurationProvider.GetRegistriesAsync(surface, cancellationToken);
        }

        return sources
            .Select(s => new OciUpstreamRegistryOptions
            {
                Url = s.FeedUrl,
                Username = secretProtector.Unprotect(s.Username),
                Password = secretProtector.Unprotect(s.Password),
                Priority = s.Priority,
            })
            .ToArray();
    }
}
