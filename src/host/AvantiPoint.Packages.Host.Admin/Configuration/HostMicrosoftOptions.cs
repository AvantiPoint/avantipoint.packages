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

    /// <summary>
    /// Entra group object IDs whose members administer this host. When configured,
    /// group membership is authoritative and administrators cannot be assigned locally.
    /// </summary>
    public List<string> AdminRoleGroupIds { get; set; } = [];

    /// <summary>
    /// Entra group object IDs whose members may publish packages. When configured,
    /// this list is authoritative and administrator membership does not imply publishing.
    /// </summary>
    public List<string> PublisherRoleGroupIds { get; set; } = [];

    /// <summary>
    /// Entra group object IDs whose members may consume packages. Administrators and
    /// publishers always retain consume access needed to manage and validate packages.
    /// </summary>
    public List<string> ConsumerRoleGroupIds { get; set; } = [];
}
