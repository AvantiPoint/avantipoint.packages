using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class RetryPublicationHandler : IFeedActionHandler
{
    public int Attempts { get; private set; }
    public int Completed { get; private set; }
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        Attempts++;
        if (Attempts == 1) throw new IOException("Transient notification failure.");
        Completed++;
        return Task.CompletedTask;
    }
}
