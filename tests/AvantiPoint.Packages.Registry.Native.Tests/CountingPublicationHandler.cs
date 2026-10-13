using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class CountingPublicationHandler : IFeedActionHandler
{
    private int _uploads;
    public int Uploads => _uploads;
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public async Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _uploads);
        await Task.Delay(100, cancellationToken);
    }
}
