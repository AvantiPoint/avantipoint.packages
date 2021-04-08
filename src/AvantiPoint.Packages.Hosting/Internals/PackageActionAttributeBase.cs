using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Hosting.Internals
{
    internal abstract class PackageActionAttributeBase : ActionFilterAttribute
    {
        public sealed override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            try
            {
                var handler = context.HttpContext.RequestServices.GetService<INuGetFeedActionHandler>();
                if (handler is not null)
                {
                    await Handle(handler, null, null);
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
    }

    internal sealed class HandlePackageDownloadedAttribute : PackageActionAttributeBase
    {
        protected override Task Handle(INuGetFeedActionHandler handler, string packageId, string packageVersion)
        {
            return handler.OnPackageDownloaded(packageId, packageVersion);
        }
    }

    internal sealed class HandlePackageUploadedAttribute : PackageActionAttributeBase
    {
        protected override Task Handle(INuGetFeedActionHandler handler, string packageId, string packageVersion)
        {
            return handler.OnPackageUploaded(packageId, packageVersion);
        }
    }

    internal sealed class HandleSymbolsUploadedAttribute : PackageActionAttributeBase
    {
        protected override Task Handle(INuGetFeedActionHandler handler, string packageId, string packageVersion)
        {
            return handler.OnSymbolsUploaded(packageId, packageVersion);
        }
    }
}
