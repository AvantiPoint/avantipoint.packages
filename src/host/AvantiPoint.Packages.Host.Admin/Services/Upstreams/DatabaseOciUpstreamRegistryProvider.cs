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
        // Scoped to this surface: a source with no Surface set applies to every OCI surface,
        // one with a Surface set only applies when it matches surface.OciSegment. Without this,
        // a host with multiple OCI surfaces would have every DB-managed OCI source apply to all
        // of them (or, worse, disabling a source meant for one surface would suppress the
        // static fallback for unrelated surfaces).
        var sources = await packageSourceService.GetEnabledUpstreamSourcesAsync(
            PackageSourceProtocol.Oci,
            surface.OciSegment,
            cancellationToken);

        if (sources.Count == 0)
        {
            // Only fall back to static configuration when no OCI sources have ever been
            // defined for this surface. If sources exist but are all disabled, respect that
            // and mirror nothing - otherwise disabling every row would silently re-enable
            // whatever static config still lists.
            var hasAnySources = await packageSourceService.HasUpstreamSourcesAsync(
                PackageSourceProtocol.Oci,
                surface.OciSegment,
                cancellationToken);

            return hasAnySources
                ? []
                : await configurationProvider.GetRegistriesAsync(surface, cancellationToken);
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
