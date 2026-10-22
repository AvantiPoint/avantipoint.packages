using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.UI.Tests;

internal sealed class NativeUiArtifactPolicy : IFeedActionHandler
{
    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(context.ArtifactName != "hidden" && context.DigestOrTarballPath?.EndsWith(".tar.gz", StringComparison.Ordinal) == true);

    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
