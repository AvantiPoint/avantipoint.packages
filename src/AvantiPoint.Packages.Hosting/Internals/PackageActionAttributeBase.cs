using System;
using System.Threading.Tasks;
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
                var handler = context.HttpContext.RequestServices.GetService<INuGetFeedActionHandler>();
                var packageId = string.Empty;
                var packageVersion = string.Empty;
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
