#nullable enable

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Options that control upstream mirroring behavior.
/// </summary>
public class MirrorOptions
{
    /// <summary>
    /// Optional path to a NuGet.config file mounted into the container or host.
    /// When set, the server will read upstream package sources from this file
    /// and ensure corresponding <see cref="PackageSource"/> rows exist.
    /// </summary>
    public string? NuGetConfigPath { get; set; }

    /// <summary>
    /// Default repository signature policy to apply to sources imported from NuGet.config.
    /// </summary>
    public MirrorRepositorySignaturePolicy DefaultSignaturePolicy { get; set; } = MirrorRepositorySignaturePolicy.Resign;

    /// <summary>
    /// Default caching strategy to apply to sources imported from NuGet.config.
    /// </summary>
    public PackageSourceCachingStrategy DefaultCachingStrategy { get; set; } = PackageSourceCachingStrategy.IndexAndCache;
}


