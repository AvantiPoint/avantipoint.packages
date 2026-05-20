using System;

namespace AvantiPoint.Packages.Core.Entities.Npm;

public class NpmVersion
{
    public int Key { get; set; }

    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;

    public int PackageKey { get; set; }

    public NpmPackage Package { get; set; }

    public string Version { get; set; }

    public string TarballPath { get; set; }

    public string Shasum { get; set; }

    public PackageOrigin Origin { get; set; } = PackageOrigin.Published;

    public string PackumentJson { get; set; }

    public DateTime Published { get; set; } = DateTime.UtcNow;
}
