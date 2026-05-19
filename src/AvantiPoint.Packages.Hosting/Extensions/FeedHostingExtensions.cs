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
    public static WebApplication MapAvantiPointNuGetFeed(this WebApplication app)
    {
        app.UseAvantiPointFeedPlatform();
        return app.MapNuGetApiRoutes();
    }

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

internal sealed class NuGetFeedActionHandlerAdapter : IFeedActionHandler
{
    private readonly INuGetFeedActionHandler _handler;

    public NuGetFeedActionHandlerAdapter(INuGetFeedActionHandler handler)
    {
        _handler = handler;
    }

    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken)
    {
        if (context.Surface.Protocol != FeedProtocol.NuGet || string.IsNullOrEmpty(context.Version))
        {
            return Task.FromResult(false);
        }

        return _handler.CanDownloadPackage(context.ArtifactName, context.Version);
    }

    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken)
    {
        if (context.Surface.Protocol != FeedProtocol.NuGet || string.IsNullOrEmpty(context.Version))
        {
            return Task.CompletedTask;
        }

        return _handler.OnPackageDownloaded(context.ArtifactName, context.Version);
    }

    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken)
    {
        if (context.Surface.Protocol != FeedProtocol.NuGet || string.IsNullOrEmpty(context.Version))
        {
            return Task.CompletedTask;
        }

        return _handler.OnPackageUploaded(context.ArtifactName, context.Version);
    }
}
