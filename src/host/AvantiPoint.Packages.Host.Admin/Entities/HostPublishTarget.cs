namespace AvantiPoint.Packages.Host.Admin.Entities;

public class HostPublishTarget
{
    public string Name { get; set; } = string.Empty;

    public string PublishEndpoint { get; set; } = string.Empty;

    public string ApiToken { get; set; } = string.Empty;

    public bool Legacy { get; set; }

    public string AddedBy { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public List<HostPackageGroupSyndication> Syndications { get; set; } = [];
}
