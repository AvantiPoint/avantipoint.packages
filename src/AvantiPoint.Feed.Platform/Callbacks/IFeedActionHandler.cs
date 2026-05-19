namespace AvantiPoint.Feed.Platform.Callbacks;

public interface IFeedActionHandler
{
    Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default);

    Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default);

    Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default);
}
