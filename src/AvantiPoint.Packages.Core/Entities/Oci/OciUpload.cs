using System;

namespace AvantiPoint.Packages.Core.Entities.Oci;

public class OciUpload
{
    public int Key { get; set; }

    public string UploadId { get; set; } = string.Empty;

    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;

    public string? OciSegment { get; set; }

    public string RepositoryName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public long BytesReceived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
