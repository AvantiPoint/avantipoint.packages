using System.Threading.Tasks;

namespace AvantiPoint.Packages.Hosting.Internals
{
    internal sealed class HandleSymbolsUploadedAttribute : PackageActionAttributeBase
    {
        protected override Task Handle(INuGetFeedActionHandler handler, string packageId, string packageVersion)
        {
            return handler.OnSymbolsUploaded(packageId, packageVersion);
        }
    }
}
