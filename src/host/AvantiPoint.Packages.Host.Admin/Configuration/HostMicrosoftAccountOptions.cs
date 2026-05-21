namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class HostMicrosoftAccountOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TenantId { get; set; } = "common";
    public List<string> AllowedEmailDomains { get; set; } = [];
}
