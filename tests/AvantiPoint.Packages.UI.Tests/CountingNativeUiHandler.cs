using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.UI.Tests;

internal sealed class CountingNativeUiHandler : IFeedActionHandler
{
    public int Calls { get; set; }
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(true);
    }
    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
