#nullable enable

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Additional context supplied when ingesting a package so the indexing pipeline can adjust behavior.
/// </summary>
public class PackageIngestionContext
{
    /// <summary>
    /// Identifies how the package entered the feed. Defaults to <see cref="PackageOrigin.Published"/>.
    /// </summary>
    public PackageOrigin Origin { get; init; } = PackageOrigin.Published;

    /// <summary>
    /// Links the package to the source it was mirrored from, if any.
    /// </summary>
    public int? PackageSourceId { get; init; }

    /// <summary>
    /// Repository signature behavior to apply for mirrored packages.
    /// </summary>
    public MirrorRepositorySignaturePolicy MirrorSignaturePolicy { get; init; } = MirrorRepositorySignaturePolicy.Resign;

    /// <summary>
    /// Captures the caching policy so downstream components can adjust (e.g., skip search indexing).
    /// </summary>
    public PackageSourceCachingStrategy? CachingStrategy { get; init; }

    /// <summary>
    /// When true, the package should not be added to the search index.
    /// </summary>
    public bool SkipSearchIndexing { get; init; }

    /// <summary>
    /// Controls whether the publish-time signature policy should be enforced.
    /// Mirror ingestion disables this to avoid double-signing.
    /// </summary>
    public bool ApplyPublishSignaturePolicy { get; init; } = true;
}

