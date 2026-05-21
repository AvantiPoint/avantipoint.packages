using System;

namespace AvantiPoint.Packages.Core.Entities.Oci;

public class OciManifest
{
    public int Key { get; set; }

    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;

    public string? OciSegment { get; set; }

    public string Digest { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public string? PlatformOs { get; set; }

    public string? PlatformArch { get; set; }

    public OciArtifactKind ArtifactKind { get; set; } = OciArtifactKind.Unknown;

    public PackageOrigin Origin { get; set; } = PackageOrigin.Published;

    public long Size { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
