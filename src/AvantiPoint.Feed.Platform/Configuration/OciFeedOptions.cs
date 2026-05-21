namespace AvantiPoint.Feed.Platform.Configuration;

public class OciFeedOptions
{
    public OciProfile Profile { get; set; } = OciProfile.General;

    public bool AllowUnknownMediaTypes { get; set; } = true;

    public bool IncludeMirroredInCatalog { get; set; }

    public OciPlatformPolicyOptions PlatformPolicy { get; set; } = new();
}
