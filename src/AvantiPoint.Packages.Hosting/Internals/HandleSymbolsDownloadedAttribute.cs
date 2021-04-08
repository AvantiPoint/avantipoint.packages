using System.Threading.Tasks;

namespace AvantiPoint.Packages.Hosting.Internals
{
    internal sealed class HandleSymbolsDownloadedAttribute : PackageActionAttributeBase
    {
        protected override Task<bool> CanHandle(INuGetFeedActionHandler handler, string packageId, string packageVersion)
        {
            return handler.CanDownloadPackage(packageId, packageVersion);
        }

        protected override Task Handle(INuGetFeedActionHandler handler, string packageId, string packageVersion)
        {
            return handler.OnSymbolsDownloaded(packageId, packageVersion);
        }
    }
}
