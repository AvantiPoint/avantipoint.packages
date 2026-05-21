namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class HostGoogleOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Google Workspace hosted domain (maps to the OAuth <c>hd</c> claim). Required for organizational sign-in.
    /// </summary>
    public string? HostedDomain { get; set; }

    /// <summary>
    /// Google Workspace group IDs or group email addresses that users must belong to.
    /// Standard Google OAuth ID tokens do not include group membership; enforcing this list requires
    /// Google Admin SDK Directory API or Cloud Identity Groups API integration (planned phase 2).
    /// Leave empty to require only <see cref="HostedDomain"/> membership.
    /// </summary>
    public List<string> RequiredGroupIds { get; set; } = [];
}
