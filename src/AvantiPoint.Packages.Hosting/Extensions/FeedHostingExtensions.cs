using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Feed.Platform.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Hosting.Extensions;

public static class FeedHostingExtensions
{
    public static WebApplication MapAvantiPointNuGetFeed(this WebApplication app) =>
        app.MapNuGetApiRoutes();

    public static IServiceCollection AddNuGetFeedActionHandlerAdapter(this IServiceCollection services)
    {
        services.AddScoped<IFeedActionHandler>(sp =>
        {
            var handlers = new List<IFeedActionHandler>();
            var nugetHandler = sp.GetService<INuGetFeedActionHandler>();
            if (nugetHandler is not null)
            {
                handlers.Add(new NuGetFeedActionHandlerAdapter(nugetHandler));
            }

            return new CompositeFeedActionHandler(handlers);
        });

        return services;
    }
}

