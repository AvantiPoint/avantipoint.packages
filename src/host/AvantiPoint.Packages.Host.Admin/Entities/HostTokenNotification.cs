namespace AvantiPoint.Packages.Host.Admin.Entities;

public class HostTokenNotification
{
    public int Id { get; set; }

    public int HostApiTokenId { get; set; }

    public string NotificationType { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }

    public HostApiToken Token { get; set; } = null!;
}
