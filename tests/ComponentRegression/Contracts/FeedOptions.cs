namespace AvantiPoint.Feed.Platform.Configuration;

// Test-only projection: the adapter only reads Authentication.AllowAnonymousPull.
public sealed class FeedOptions
{
    public FeedAuthenticationOptions Authentication { get; set; } = new();
}

public sealed class FeedAuthenticationOptions
{
    public bool AllowAnonymousPull { get; set; }
}
