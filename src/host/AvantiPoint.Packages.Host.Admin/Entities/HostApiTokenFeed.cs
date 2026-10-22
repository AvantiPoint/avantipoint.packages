namespace AvantiPoint.Packages.Host.Admin.Entities;

/// <summary>An explicitly selected feed and ecosystem endpoint accessible to a token.</summary>
public sealed class HostApiTokenFeed
{
    public int HostApiTokenId { get; set; }
    public HostApiToken Token { get; set; } = null!;
    public string FeedId { get; set; } = string.Empty;
    public string SurfaceId { get; set; } = string.Empty;
}
