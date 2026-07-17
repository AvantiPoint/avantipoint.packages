using AvantiPoint.Feed.Platform.Callbacks;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services;

/// <summary>
/// The outcome of promoting a package group to a publish target. A package counts as
/// pushed only when its primary package upload succeeds; a missing/failed symbols upload
/// does not fail the package (many packages have no symbols to push).
/// </summary>
public sealed record SyndicationPushResult(
    IReadOnlyList<string> PushedPackageIds,
    IReadOnlyList<string> FailedPackageIds)
{
    public bool AllSucceeded => FailedPackageIds.Count == 0;
}

public interface ISyndicationService
{
    Task SyndicatePackageAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken = default);

    Task SyndicateSymbolsAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Automatically publishes an npm or OCI artifact to matching targets configured for its
    /// package groups.
    /// </summary>
    Task SyndicateArtifactAsync(
        FeedArtifactEventContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes every member of <paramref name="groupName"/> to <paramref name="targetName"/>.
    /// Throws <see cref="InvalidOperationException"/> if the group or target does not exist.
    /// </summary>
    Task<SyndicationPushResult> PushToSourceAsync(string groupName, string targetName, CancellationToken cancellationToken = default);
}
