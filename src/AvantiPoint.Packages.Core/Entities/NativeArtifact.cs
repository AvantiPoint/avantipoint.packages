using System;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Transactional logical identity pointing to immutable, content-addressed bytes.
/// Native metadata and payloads share the same feed-isolation and conflict rules.
/// </summary>
public sealed class NativeArtifact
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FeedId { get; set; }
    public string Protocol { get; set; }
    public string Path { get; set; }
    public string PathHash { get; set; }
    public string ContentHash { get; set; }
    public string ContentType { get; set; }
    public long Length { get; set; }
    public string PackageName { get; set; }
    public string Version { get; set; }
    public string MetadataJson { get; set; }
    public DateTime PublishedUtc { get; set; }
}
