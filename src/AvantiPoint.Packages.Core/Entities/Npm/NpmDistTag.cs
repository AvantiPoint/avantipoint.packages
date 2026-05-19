namespace AvantiPoint.Packages.Core.Entities.Npm;

public class NpmDistTag
{
    public int Key { get; set; }

    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;

    public int PackageKey { get; set; }

    public NpmPackage Package { get; set; }

    public string Tag { get; set; }

    public string Version { get; set; }
}
