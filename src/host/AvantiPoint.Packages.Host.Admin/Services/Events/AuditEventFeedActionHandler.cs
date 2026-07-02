using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Host.Admin.Services.Events;

/// <summary>
/// Records a <c>package.published</c> audit event (and queues it for webhook delivery) for every
/// non-NuGet upload. NuGet uploads are already recorded by <c>HostNuGetFeedActionHandler</c> via the
/// NuGet-specific <see cref="IFeedActionHandler"/> adapter; recording them again here would duplicate
/// the event.
/// </summary>
public sealed class AuditEventFeedActionHandler(IHostEventService eventService) : IProtocolNeutralFeedActionHandler
{
    // CompositeFeedActionHandler excludes IProtocolNeutralFeedActionHandler instances from access
    // decisions entirely, so this is never actually invoked; it exists only to satisfy the interface.
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        if (context.Surface.Protocol == FeedProtocol.NuGet || string.IsNullOrEmpty(context.Version))
        {
            return Task.CompletedTask;
        }

        return eventService.RecordAsync(
            "package.published",
            context.ArtifactName,
            $"protocol={context.Surface.Protocol}; version={context.Version}",
            cancellationToken);
    }
}
