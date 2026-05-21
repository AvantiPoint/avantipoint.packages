#nullable enable

namespace AvantiPoint.Packages.Core.Entities.Oci;

public class OciTag
{
    public int Key { get; set; }

    public string FeedId { get; set; } = FeedConstants.DefaultFeedId;

    public string? OciSegment { get; set; }

    public int RepositoryKey { get; set; }

    public OciRepository Repository { get; set; } = null!;

    public string Tag { get; set; } = string.Empty;

    public string ManifestDigest { get; set; } = string.Empty;

    public PackageOrigin Origin { get; set; } = PackageOrigin.Published;
}
