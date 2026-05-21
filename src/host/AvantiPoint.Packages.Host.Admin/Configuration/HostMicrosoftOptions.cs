namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class HostMicrosoftOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Directory tenant ID for organizational sign-in. Must not be "common", "consumers", or "organizations".
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    public List<string> AllowedEmailDomains { get; set; } = [];

    public List<string> RequiredGroupIds { get; set; } = [];

    public List<string> AdminRoleGroupIds { get; set; } = [];

    public List<string> PublisherRoleGroupIds { get; set; } = [];

    public List<string> ConsumerRoleGroupIds { get; set; } = [];
}
