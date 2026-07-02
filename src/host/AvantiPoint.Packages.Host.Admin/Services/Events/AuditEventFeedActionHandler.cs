using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Host.Admin.Services.Events;

/// <summary>
/// Records a <c>package.published</c> audit event (and queues it for webhook delivery) for every
/// upload that invokes <see cref="IFeedActionHandler"/> and isn't NuGet - today that's npm only.
/// NuGet uploads are already recorded by <c>HostNuGetFeedActionHandler</c> via the NuGet-specific
/// adapter; recording them again here would duplicate the event. OCI uploads are not covered: the
/// OCI registry endpoints (<c>AvantiPoint.Packages.Registry.Oci</c>) don't invoke
/// <see cref="IFeedActionHandler"/> at all yet, so OCI publishes produce no audit/webhook events
/// until that plumbing is added.
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
