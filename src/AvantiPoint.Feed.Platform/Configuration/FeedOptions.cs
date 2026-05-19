namespace AvantiPoint.Feed.Platform.Configuration;

public class FeedOptions
{
    public string Name { get; set; } = "default";

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
    public NpmMirrorOptions Mirror { get; set; } = new();
}

public class NpmMirrorOptions
{
    public string RegistryUrl { get; set; } = "https://registry.npmjs.org";
}
