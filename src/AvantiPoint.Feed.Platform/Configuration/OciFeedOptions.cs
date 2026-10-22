namespace AvantiPoint.Feed.Platform.Configuration;

public class OciFeedOptions
{
    /// <summary>
    /// Maximum body size for one authenticated blob upload request (5 GiB by default).
    /// OCI uploads stream to storage; this is not a memory allocation or total image-size limit.
    /// </summary>
    public long MaxUploadRequestBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>
    /// When true, registers this OCI feed surface and OCI registry endpoints.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Optional human-facing name for this registry surface. When omitted, the UI
    /// uses Docker, Helm, or OCI according to <see cref="Profile"/>.
    /// </summary>
    public string? DisplayName { get; set; }

    public OciProfile Profile { get; set; } = OciProfile.General;

    public bool AllowUnknownMediaTypes { get; set; } = true;

    public bool IncludeMirroredInCatalog { get; set; }

    public OciMirrorOptions Mirror { get; set; } = new();

    public OciPlatformPolicyOptions PlatformPolicy { get; set; } = new();

    public OciVulnerabilityPolicyOptions VulnerabilityPolicy { get; set; } = new();
}
