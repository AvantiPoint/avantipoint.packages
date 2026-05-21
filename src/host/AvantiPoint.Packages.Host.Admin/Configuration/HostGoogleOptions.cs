namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class HostGoogleOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? HostedDomain { get; set; }
}
