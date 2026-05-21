using NuGet.Versioning;

namespace AvantiPoint.Packages.Core.Maintenance
{
    /// <summary>
    /// Simple package identifier for backfill operations.
    /// </summary>
    internal class PackageIdentifier
    {
        public string Id { get; set; }
        public NuGetVersion Version { get; set; }
    }
}
