using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class CanonicalPubVersionPolicy : IFeedActionHandler
{
    public bool DenyRelease { get; set; }
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(!DenyRelease || context.Version is not "1.0.0" and not "1.2.3-1.dev+pre.2");
    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
