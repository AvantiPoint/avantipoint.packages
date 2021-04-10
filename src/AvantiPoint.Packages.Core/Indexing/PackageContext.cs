namespace AvantiPoint.Packages.Core
{
    public class PackageContext : IPackageContext
    {
        public string PackageId { get; set; }

        public string PackageVersion { get; set; }
    }
}
