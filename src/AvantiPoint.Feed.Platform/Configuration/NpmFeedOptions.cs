namespace AvantiPoint.Feed.Platform.Configuration;

public class NpmFeedOptions
{
    /// <summary>
    /// When true, registers the npm feed surface and npm registry endpoints.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Maximum allowed npm publish HTTP body size in bytes. Default: 100 MB.
    /// </summary>
    public long MaxPublishBodyBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Maximum allowed decoded tarball size in bytes. Default: 100 MB.
    /// </summary>
    public long MaxTarballBytes { get; set; } = 100 * 1024 * 1024;

    public NpmMirrorOptions Mirror { get; set; } = new();
}
