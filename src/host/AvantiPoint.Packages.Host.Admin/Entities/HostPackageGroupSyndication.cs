namespace AvantiPoint.Packages.Host.Admin.Entities;

public class HostPackageGroupSyndication
{
    public string PackageGroupName { get; set; } = string.Empty;

    public string PublishTargetName { get; set; } = string.Empty;

    public HostPackageGroup PackageGroup { get; set; } = null!;

    public HostPublishTarget PublishTarget { get; set; } = null!;
}
