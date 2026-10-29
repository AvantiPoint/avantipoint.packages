using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.UI.Tests;

internal sealed class NativeCatalogVisibilityPolicy : IFeedActionHandler
{
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(!context.ArtifactName.StartsWith("hidden_", StringComparison.Ordinal));

    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
