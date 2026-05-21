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
            // Abstain for other protocols so npm/OCI handlers can decide without implicit deny.
            return Task.FromResult(true);
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
