namespace AvantiPoint.Packages.Host.Admin.Entities;

public class HostApiToken
{
    public int Id { get; set; }

    public string TokenPrefix { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public FeedTokenScope Scopes { get; set; } = FeedTokenScope.ReadWrite;

    public DateTimeOffset Created { get; set; }

    public DateTimeOffset Expires { get; set; }

    public bool Revoked { get; set; }

    public bool IsSystemToken { get; set; }

    public HostUser User { get; set; } = null!;

    public bool IsValid() => !Revoked && DateTimeOffset.UtcNow <= Expires;
}
