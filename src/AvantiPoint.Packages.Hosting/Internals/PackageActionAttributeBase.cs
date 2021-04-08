using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
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
                if(context.Result is NuGetAutheticationActionResult)
                {
                    return;
                }

                var statusCode = context.HttpContext.Response.StatusCode;
                if(statusCode != 200 && statusCode != 201)
                {
                    return;
                }

                var routeValues = context.ActionDescriptor.RouteValues;
                if(!routeValues.TryGetValue("id", out var packageId)
                    || !routeValues.TryGetValue("version", out var packageVersion))
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
