namespace AvantiPoint.Packages.Host.Admin.Entities;

public class HostUser
{
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public bool CanPublish { get; set; }

    public bool CanConsume { get; set; } = true;

    public bool IsRevoked { get; set; }

    public HostUserApprovalStatus ApprovalStatus { get; set; } = HostUserApprovalStatus.Approved;

    public HostExternalAuthProvider ExternalProvider { get; set; }

    public string? ExternalSubjectId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAt { get; set; }

    public List<HostApiToken> Tokens { get; set; } = [];
}
