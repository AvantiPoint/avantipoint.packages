using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class NativeMetadataVisibilityPolicy : IFeedActionHandler
{
    public HashSet<string> DeniedPaths { get; } = new(StringComparer.Ordinal);

    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(!DeniedPaths.Contains(context.DigestOrTarballPath ?? string.Empty));

    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
