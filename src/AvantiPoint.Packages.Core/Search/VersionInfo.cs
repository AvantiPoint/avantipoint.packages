using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    internal class VersionInfo
    {
        public NuGetVersion Version { get; set; }
        public int Downloads { get; set; }
    }
}
