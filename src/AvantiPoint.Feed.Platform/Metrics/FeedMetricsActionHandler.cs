using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Feed.Platform.Metrics;

/// <summary>
/// Records completed artifact operations after protocol handlers have committed the operation.
/// The handler has no access-control opinion and is excluded from authorization voting.
/// </summary>
public sealed class FeedMetricsActionHandler(FeedMetricsService metrics) : IProtocolNeutralFeedActionHandler
{
    public Task<bool> CanAccessArtifact(
        FeedArtifactEventContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task OnArtifactDownloaded(
        FeedArtifactEventContext context,
        CancellationToken cancellationToken = default)
    {
        metrics.RecordPull(context.Surface);
        return Task.CompletedTask;
    }

    public Task OnArtifactUploaded(
        FeedArtifactEventContext context,
        CancellationToken cancellationToken = default)
    {
        metrics.RecordPush(context.Surface);
        return Task.CompletedTask;
    }
}
