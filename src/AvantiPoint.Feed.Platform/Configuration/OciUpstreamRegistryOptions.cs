namespace AvantiPoint.Feed.Platform.Configuration;

public class OciUpstreamRegistryOptions
{
    public string Url { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public int Priority { get; set; }
}
