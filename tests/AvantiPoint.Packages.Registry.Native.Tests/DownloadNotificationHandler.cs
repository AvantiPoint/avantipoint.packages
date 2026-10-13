using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class DownloadNotificationHandler : IFeedActionHandler
{
    public int Downloads { get; private set; }
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        Downloads++;
        return Task.CompletedTask;
    }
}
