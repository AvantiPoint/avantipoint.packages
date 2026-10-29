using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class MavenPluginMetadataAuthorizationHandler : IFeedActionHandler
{
    public List<FeedArtifactEventContext> Authorizations { get; } = [];

    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        Authorizations.Add(context);
        return Task.FromResult(context.ArtifactName == "com.example:sdk"
            && context.DigestOrTarballPath == "com/example/maven-metadata.xml");
    }

    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
