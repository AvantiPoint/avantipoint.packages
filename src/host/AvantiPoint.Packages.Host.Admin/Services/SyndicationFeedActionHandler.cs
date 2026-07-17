using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services;

/// <summary>
/// Connects protocol-neutral npm and OCI upload events to automatic syndication. NuGet uploads
/// retain their existing adapter because symbol syndication is a NuGet-specific second event.
/// </summary>
public sealed class SyndicationFeedActionHandler(
    ISyndicationService syndicationService,
    ILogger<SyndicationFeedActionHandler> logger) : IProtocolNeutralFeedActionHandler
{
    public Task<bool> CanAccessArtifact(
        FeedArtifactEventContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task OnArtifactDownloaded(
        FeedArtifactEventContext context,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task OnArtifactUploaded(
        FeedArtifactEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Surface.Protocol is not (FeedProtocol.Npm or FeedProtocol.Oci))
        {
            return;
        }

        try
        {
            await syndicationService.SyndicateArtifactAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The source upload has already committed. Downstream availability must not change
            // the response returned to the publisher.
            logger.LogError(
                exception,
                "Auto-syndication failed for {Protocol} artifact {Artifact} {Version}",
                context.Surface.Protocol,
                context.ArtifactName,
                context.Version);
        }
    }
}
