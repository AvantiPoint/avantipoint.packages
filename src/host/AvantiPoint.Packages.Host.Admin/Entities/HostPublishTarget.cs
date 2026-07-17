namespace AvantiPoint.Packages.Host.Admin.Entities;

public class HostPublishTarget
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The registry protocol this target speaks. Defaults to NuGet.
    /// </summary>
    public PublishTargetProtocol Protocol { get; set; } = PublishTargetProtocol.NuGet;

    public string PublishEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional username for OCI Basic authentication and Bearer token exchanges. When omitted,
    /// the API token is sent directly as a Bearer token.
    /// </summary>
    public string? Username { get; set; }

    public string ApiToken { get; set; } = string.Empty;

    public bool Legacy { get; set; }

    public string AddedBy { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public List<HostPackageGroupSyndication> Syndications { get; set; } = [];
}
