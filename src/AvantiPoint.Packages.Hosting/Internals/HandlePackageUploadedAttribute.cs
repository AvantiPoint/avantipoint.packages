using System.Threading.Tasks;

namespace AvantiPoint.Packages.Hosting.Internals
{
    internal sealed class HandlePackageUploadedAttribute : PackageActionAttributeBase
    {
        protected override Task Handle(INuGetFeedActionHandler handler, string packageId, string packageVersion)
        {
            return handler.OnPackageUploaded(packageId, packageVersion);
        }
    }
}
