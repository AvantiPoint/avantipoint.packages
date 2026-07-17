using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Host.Admin.Entities;

namespace AvantiPoint.Packages.Host.Admin.Services.Publishers;

/// <summary>
/// Publishes a locally hosted package to an external downstream registry. One implementation
/// exists per <see cref="PublishTargetProtocol"/>; <see cref="SyndicationService"/> routes by
/// the target's protocol.
/// </summary>
public interface IDownstreamPublisher
{
    PublishTargetProtocol Protocol { get; }

    /// <summary>
    /// Pushes the newest (or specified) version of the package to the target.
    /// Returns false when the package does not exist locally or the push fails.
    /// </summary>
    Task<bool> PushAsync(
        DownstreamPublishRequest request,
        HostPublishTarget target,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Identifies a locally hosted artifact to publish. <see cref="SourceSurface"/> is populated for
/// automatic syndication so multi-feed and named OCI surfaces remain unambiguous. Manual group
/// promotion may omit it when the artifact name resolves to a single local source.
/// </summary>
public sealed record DownstreamPublishRequest(
    string ArtifactName,
    string? Version = null,
    SurfaceContext? SourceSurface = null);
