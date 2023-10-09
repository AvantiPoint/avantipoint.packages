using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Internals;

internal sealed class HandleSymbolsUploadedFilter : PackageActionFilter
{
    public HandleSymbolsUploadedFilter(INuGetFeedActionHandler handler, ILogger<HandleSymbolsUploadedFilter> logger, IPackageContext packageContext)
        : base(handler, logger, packageContext)
    {
    }

    protected override async ValueTask Handle(string packageId, string packageVersion) => 
        await Handler.OnSymbolsUploaded(packageId, packageVersion);
}
