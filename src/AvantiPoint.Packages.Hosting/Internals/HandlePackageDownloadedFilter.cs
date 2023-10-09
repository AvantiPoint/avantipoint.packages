using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Internals;

internal sealed class HandlePackageDownloadedFilter : PackageActionFilter
{
    public HandlePackageDownloadedFilter(INuGetFeedActionHandler handler, ILogger<HandlePackageDownloadedFilter> logger, IPackageContext packageContext) 
        : base(handler, logger, packageContext)
    {
    }

    protected override async ValueTask<bool> CanHandle(string packageId, string packageVersion) =>
        await Handler.CanDownloadPackage(packageId, packageVersion);

    protected override async ValueTask Handle(string packageId, string packageVersion) => 
        await Handler.OnPackageDownloaded(packageId, packageVersion);
}
