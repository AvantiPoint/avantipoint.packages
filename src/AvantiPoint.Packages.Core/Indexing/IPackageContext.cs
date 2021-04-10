namespace AvantiPoint.Packages.Core
{
    public interface IPackageContext
    {
        string PackageId { get; set; }

        string PackageVersion { get; set; }
    }
}
