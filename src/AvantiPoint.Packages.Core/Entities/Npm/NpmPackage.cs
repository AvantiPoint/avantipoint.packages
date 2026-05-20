using System;
using System.Collections.Generic;

namespace AvantiPoint.Packages.Core.Entities.Npm;

public class NpmPackage
{
    public int Key { get; set; }

    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;

    public string Name { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<NpmVersion> Versions { get; set; } = [];

    public ICollection<NpmDistTag> DistTags { get; set; } = [];
}
