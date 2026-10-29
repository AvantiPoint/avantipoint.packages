using AvantiPoint.Feed.Platform.Callbacks;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class LogicalPathArtifactHandler : IFeedActionHandler
{
    public bool Deny { get; set; }
    public bool RequireLogicalPath { get; set; } = true;
    public List<FeedArtifactEventContext> Uploads { get; } = [];
    public List<FeedArtifactEventContext> Downloads { get; } = [];

    public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        var path = context.DigestOrTarballPath;
        return Task.FromResult(!Deny && (!RequireLogicalPath || path is not null && !path.StartsWith("sha256:", StringComparison.Ordinal)));
    }

    public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        Uploads.Add(context);
        return Task.CompletedTask;
    }

    public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        Downloads.Add(context);
        return Task.CompletedTask;
    }
}
