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
    /// Returns all enabled sources that can act as upstream feeds (Type = Upstream or Both).
    /// </summary>
    Task<IReadOnlyList<PackageSource>> GetEnabledUpstreamSourcesAsync(CancellationToken cancellationToken = default);

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

