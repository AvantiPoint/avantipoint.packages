namespace AvantiPoint.Packages.Host.Admin.Configuration;

public class HostGitHubOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? Organization { get; set; }

    /// <summary>
    /// Optional. When set, the user must belong to at least one of these team slugs in <see cref="Organization"/>.
    /// </summary>
    public List<string> TeamSlugs { get; set; } = [];
}
