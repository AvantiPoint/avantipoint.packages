namespace AvantiPoint.Feed.Platform.Configuration;

public class FeedOptions
{
    public string Name { get; set; } = "default";

    /// <summary>
    /// Optional absolute public base URL for feed links (reverse proxy / path-prefix scenarios).
    /// Example: https://packages.example.com/myfeed
    /// </summary>
    public string PublicBaseUrl { get; set; }

    public FeedStorageOptions Storage { get; set; } = new();

    public FeedAuthenticationOptions Authentication { get; set; } = new();

    public NpmFeedOptions Npm { get; set; } = new();
}

public class FeedStorageOptions
{
    public string Prefix { get; set; } = "feeds/default/";
}

public class FeedAuthenticationOptions
{
    public string ApiKey { get; set; }

    public bool AllowAnonymousPull { get; set; }
}

public class NpmFeedOptions
{
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

public class NpmMirrorOptions
{
    public string RegistryUrl { get; set; } = "https://registry.npmjs.org";
}
