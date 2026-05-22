namespace AvantiPoint.Feed.Platform.Configuration;

public class OciFeedOptions
{
    /// <summary>
    /// When true, registers this OCI feed surface and OCI registry endpoints.
    /// </summary>
    public bool Enabled { get; set; }

    public OciProfile Profile { get; set; } = OciProfile.General;

    public bool AllowUnknownMediaTypes { get; set; } = true;

    public bool IncludeMirroredInCatalog { get; set; }

    public OciMirrorOptions Mirror { get; set; } = new();

    public OciPlatformPolicyOptions PlatformPolicy { get; set; } = new();
}
