namespace AvantiPoint.Packages.Host.Admin.Entities;

public class HostPackageGroupMember
{
    public string PackageGroupName { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public HostPackageGroup PackageGroup { get; set; } = null!;
}
