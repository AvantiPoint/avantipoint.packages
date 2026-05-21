namespace AvantiPoint.Packages.Host.Admin.Entities;

public class HostPackageGroup
{
    public string Name { get; set; } = string.Empty;

    public List<HostPackageGroupMember> Members { get; set; } = [];

    public List<HostPackageGroupSyndication> Syndications { get; set; } = [];
}
