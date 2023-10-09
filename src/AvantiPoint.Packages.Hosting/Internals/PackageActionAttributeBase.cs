using System;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Internals
{
    internal abstract class PackageActionAttributeBase : ActionFilterAttribute
    {
        public sealed override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            try
            {
                var statusCode = context.HttpContext.Response.StatusCode;
                if(statusCode != 200 && statusCode != 201)
                {
                    return;
                }

                var packageContext = context.HttpContext.RequestServices.GetRequiredService<IPackageContext>();
                var packageId = packageContext.PackageId;
                var packageVersion = packageContext.PackageVersion;
                if(string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(packageVersion))
                {
                    return;
                }

                var handler = context.HttpContext.RequestServices.GetService<INuGetFeedActionHandler>();
                if (handler is not null && await handler.CanDownloadPackage(packageId, packageVersion))
                {
                    await Handle(handler, packageId, packageVersion);
                }
            }
            catch (Exception ex)
            {
                var factory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = factory.CreateLogger(GetType().Name);
                logger.LogError(ex, "Error proccessing NuGet Action Callback");
            }

            await base.OnResultExecutionAsync(context, next);
        }

        protected abstract Task Handle(INuGetFeedActionHandler handler, string packageId, string packageVersion);

        protected virtual Task<bool> CanHandle(INuGetFeedActionHandler handler, string packageId, string packageVersion) =>
            Task.FromResult(true);
    }
}
