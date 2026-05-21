using System;

namespace AvantiPoint.Packages.Core.Entities.Oci;

public class OciBlob
{
    public int Key { get; set; }

    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;

    public string? OciSegment { get; set; }

    public string Digest { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
