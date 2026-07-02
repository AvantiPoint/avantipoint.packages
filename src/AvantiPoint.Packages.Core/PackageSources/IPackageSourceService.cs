#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Persists and queries package sources that drive upstream mirroring and downstream publishing.
/// </summary>
public interface IPackageSourceService
{
    /// <summary>
    /// Returns all enabled NuGet sources that can act as upstream feeds (Type = Upstream or Both).
    /// </summary>
    Task<IReadOnlyList<PackageSource>> GetEnabledUpstreamSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all enabled sources for the given protocol that can act as upstream feeds
    /// (Type = Upstream or Both), ordered by priority. Equivalent to
    /// <see cref="GetEnabledUpstreamSourcesAsync(PackageSourceProtocol, string, CancellationToken)"/>
    /// with a null surface (matches only sources that are not scoped to a specific surface).
    /// </summary>
    Task<IReadOnlyList<PackageSource>> GetEnabledUpstreamSourcesAsync(PackageSourceProtocol protocol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all enabled sources for the given protocol and surface that can act as upstream
    /// feeds (Type = Upstream or Both), ordered by priority. A source with a null
    /// <see cref="PackageSource.Surface"/> applies to every surface; a source scoped to a
    /// specific surface only applies when <paramref name="surface"/> matches it.
    /// </summary>
    /// <param name="surface">
    /// The named surface to resolve sources for (for OCI, the segment name), or null for the
    /// default/unsegmented surface.
    /// </param>
    Task<IReadOnlyList<PackageSource>> GetEnabledUpstreamSourcesAsync(PackageSourceProtocol protocol, string? surface, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any upstream/both source is defined for the given protocol, regardless of
    /// <see cref="PackageSource.IsEnabled"/>. Callers use this to distinguish "no sources have
    /// ever been configured" (fall back to static configuration) from "sources exist but are
    /// all disabled" (respect that and mirror nothing). Equivalent to
    /// <see cref="HasUpstreamSourcesAsync(PackageSourceProtocol, string, CancellationToken)"/>
    /// with a null surface.
    /// </summary>
    Task<bool> HasUpstreamSourcesAsync(PackageSourceProtocol protocol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any upstream/both source is defined for the given protocol and surface,
    /// regardless of <see cref="PackageSource.IsEnabled"/>. A source with a null
    /// <see cref="PackageSource.Surface"/> counts for every surface.
    /// </summary>
    Task<bool> HasUpstreamSourcesAsync(PackageSourceProtocol protocol, string? surface, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a specific source or throws if it cannot be found.
    /// </summary>
    Task<PackageSource> GetRequiredAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new package source definition.
    /// </summary>
    Task<PackageSource> AddAsync(PackageSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing package source definition.
    /// </summary>
    Task<PackageSource> UpdateAsync(PackageSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes cached metadata for a source by probing the upstream service index.
    /// </summary>
    Task<PackageSourceMetadata> RefreshMetadataAsync(int sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a sync attempt for monitoring and troubleshooting.
    /// </summary>
    Task UpdateSyncStateAsync(int sourceId, bool success, string? error, CancellationToken cancellationToken = default);
}

