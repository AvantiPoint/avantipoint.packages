using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Internals;

internal abstract class PackageActionFilter : IEndpointFilter
{
    protected INuGetFeedActionHandler Handler { get; }
    protected ILogger Logger { get; }
    private IPackageContext _packageContext { get; }

    protected PackageActionFilter(INuGetFeedActionHandler handler, ILogger logger, IPackageContext packageContext)
    {
        Handler = handler;
        Logger = logger;
        _packageContext = packageContext;
    }

    protected HttpContext HttpContext { get; private set; }

    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext = context.HttpContext;

        var result = await next(context);

        var packageId = _packageContext.PackageId;
        var packageVersion = _packageContext.PackageVersion;
        if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(packageVersion))
        {
            return result;
        }

        switch (result)
        {
            case FileResult:
            case StatusCodeResult status when status.StatusCode == 200 || status.StatusCode == 201:
                if (await CanHandle(packageId, packageVersion))
                {
                    await Handle(packageId, packageVersion);
                }
                break;
        }

        return result;
    }

    protected abstract ValueTask Handle(string packageId, string packageVersion);

    protected virtual ValueTask<bool> CanHandle(string packageId, string packageVersion) =>
        ValueTask.FromResult(true);
}
