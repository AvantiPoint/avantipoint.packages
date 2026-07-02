using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Services.Upstreams;

/// <summary>
/// Resolves upstream npm registries from the database (<see cref="PackageSource"/> rows with
/// <see cref="PackageSourceProtocol.Npm"/>) so they can be managed at runtime from the admin UI.
/// Falls back to static configuration when no database sources are defined. The
/// <see cref="PackageSource.ApiKey"/> column doubles as the npm bearer token.
/// </summary>
public sealed class DatabaseNpmUpstreamRegistryProvider(
    IPackageSourceService packageSourceService,
    ISecretProtector secretProtector,
    IOptions<NpmFeedOptions> options) : INpmUpstreamRegistryProvider
{
    public async ValueTask<IReadOnlyList<NpmUpstreamRegistryOptions>> GetRegistriesAsync(
        CancellationToken cancellationToken = default)
    {
        var sources = await packageSourceService.GetEnabledUpstreamSourcesAsync(
            PackageSourceProtocol.Npm,
            cancellationToken);

        if (sources.Count == 0)
        {
            // Only fall back to static configuration when no npm sources have ever been
            // defined. If sources exist but are all disabled, respect that and mirror
            // nothing - otherwise disabling every row would silently re-enable the
            // (potentially unauthenticated) default registry.
            var hasAnySources = await packageSourceService.HasUpstreamSourcesAsync(
                PackageSourceProtocol.Npm,
                cancellationToken);

            return hasAnySources
                ? []
                : options.Value.Mirror?.GetUpstreamRegistries() ?? [];
        }

        return sources
            .Select(s => new NpmUpstreamRegistryOptions
            {
                Url = s.FeedUrl,
                Token = secretProtector.Unprotect(s.ApiKey),
                Username = secretProtector.Unprotect(s.Username),
                Password = secretProtector.Unprotect(s.Password),
                Priority = s.Priority,
            })
            .ToArray();
    }
}
