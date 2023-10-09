using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Internals;

internal sealed class HandlePackageUploadedFilter : PackageActionFilter
{
    public HandlePackageUploadedFilter(INuGetFeedActionHandler handler, ILogger<HandlePackageUploadedFilter> logger, IPackageContext packageContext)
        : base(handler, logger, packageContext)
    {
    }

    protected override async ValueTask Handle(string packageId, string packageVersion) => 
        await Handler.OnPackageUploaded(packageId, packageVersion);
}
