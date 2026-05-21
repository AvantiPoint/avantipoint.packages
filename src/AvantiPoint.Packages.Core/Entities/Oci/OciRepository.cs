using System;
using System.Collections.Generic;

namespace AvantiPoint.Packages.Core.Entities.Oci;

public class OciRepository
{
    public int Key { get; set; }

    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;

    /// <summary>
    /// Named OCI segment (e.g. "helm", "docker"). Null for the default /v2/ registration.
    /// </summary>
    public string? OciSegment { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OciTag> Tags { get; set; } = [];
}
