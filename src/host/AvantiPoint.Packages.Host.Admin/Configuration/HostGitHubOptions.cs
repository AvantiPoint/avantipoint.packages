namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class HostGitHubOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? Organization { get; set; }
}
