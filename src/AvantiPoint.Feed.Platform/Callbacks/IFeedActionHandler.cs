namespace AvantiPoint.Feed.Platform.Callbacks;

public interface IFeedActionHandler
{
    /// <summary>
    /// Returns whether the current user may access the artifact. When false, the feed must not serve the artifact (403).
    /// Handlers that do not apply to the current protocol should return true to abstain.
    /// </summary>
    Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default);

    Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default);

    Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default);
}
