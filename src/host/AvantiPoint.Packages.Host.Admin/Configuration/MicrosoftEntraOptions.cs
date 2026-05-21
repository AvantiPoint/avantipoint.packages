namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class MicrosoftEntraOptions
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-oidc";
    public List<string> RequiredGroupIds { get; set; } = [];
    public List<string> AdminRoleGroupIds { get; set; } = [];
    public List<string> PublisherRoleGroupIds { get; set; } = [];
    public List<string> ConsumerRoleGroupIds { get; set; } = [];
}
