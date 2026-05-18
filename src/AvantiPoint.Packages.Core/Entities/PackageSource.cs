using System;
using System.Collections.Generic;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Represents an upstream or downstream feed that can participate in mirroring or publishing flows.
/// </summary>
public class PackageSource
{
    public int Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Absolute URL to the upstream service index (<c>/v3/index.json</c>).
    /// </summary>
    public string FeedUrl { get; set; }

    /// <summary>
    /// Indicates whether the feed can be used for upstream mirroring, downstream publishing, or both.
    /// </summary>
    public PackageSourceType Type { get; set; } = PackageSourceType.Upstream;

    /// <summary>
    /// Controls how this source handles mirrored packages: fully indexed, cached only, or proxied.
    /// </summary>
    public PackageSourceCachingStrategy CachingStrategy { get; set; } = PackageSourceCachingStrategy.IndexAndCache;

    /// <summary>
    /// Optional username for authenticated feeds that use basic authentication.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Optional password or access token for basic authentication feeds.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Optional API key header for feeds that rely on <c>X-NuGet-ApiKey</c>.
    /// </summary>
    public string ApiKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Repository signature handling mode for mirrored packages originating from this source.
    /// </summary>
    public MirrorRepositorySignaturePolicy MirrorSignaturePolicy { get; set; } = MirrorRepositorySignaturePolicy.Resign;

    /// <summary>
    /// Additional metadata discovered during probing of the upstream service index.
    /// </summary>
    public PackageSourceMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Audit timestamps and sync state.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastModifiedAt { get; set; }

    public DateTimeOffset? LastSyncAttemptAt { get; set; }

    public DateTimeOffset? LastSyncSuccessAt { get; set; }

    public string LastError { get; set; }

    /// <summary>
    /// Packages that originated from this source.
    /// </summary>
    public ICollection<Package> Packages { get; set; } = new List<Package>();
}
