using System;
using System.Threading.Tasks;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using FeedProtocol = AvantiPoint.Feed.Platform.FeedProtocol;
using AvantiPoint.Packages.Core;
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

                var surfaceAccessor = context.HttpContext.RequestServices.GetService<ISurfaceContextAccessor>();
                var surface = surfaceAccessor?.Current ?? new SurfaceContext(
                    FeedConstants.DefaultFeedId,
                    FeedProtocol.NuGet,
                    "nuget",
                    OciSegment: null,
                    RoutePrefix: string.Empty,
                    new Uri($"{context.HttpContext.Request.Scheme}://{context.HttpContext.Request.Host}"));

                var feedHandler = context.HttpContext.RequestServices.GetService<IFeedActionHandler>();
                var eventContext = new FeedArtifactEventContext(surface, packageId, packageVersion, null);
                if (feedHandler is not null && await feedHandler.CanAccessArtifact(eventContext))
                {
                    await Handle(feedHandler, eventContext);
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

        protected abstract Task Handle(IFeedActionHandler handler, FeedArtifactEventContext context);
    }
}
