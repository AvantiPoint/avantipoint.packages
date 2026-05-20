using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Registry.Npm.Tests;

internal sealed class DenyNpmArtifactHandler : IFeedActionHandler
{
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
