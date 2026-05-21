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

