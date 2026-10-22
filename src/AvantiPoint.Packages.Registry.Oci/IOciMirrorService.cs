using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Registry.Oci;

public interface IOciMirrorService
{
    Task<OciUpstreamManifest?> TryFetchManifestAsync(
        SurfaceContext surface,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one bounded tags page for a repository that is already tracked locally. The
    /// registry implementation deliberately has no catalog-enumeration operation.
    /// </summary>
    Task<OciUpstreamTagList?> TryListTagsAsync(
        SurfaceContext surface,
        string repositoryName,
        int maximumTags,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<OciUpstreamTagList?>(null);

    Task<Stream?> TryFetchBlobAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default);

    async Task<OciUpstreamBlob?> TryFetchBlobWithPolicyAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default)
    {
        var content = await TryFetchBlobAsync(surface, repositoryName, digest, cancellationToken);
        return content is null
            ? null
            : new OciUpstreamBlob(content, Strategy(surface))
            {
                Size = content.CanSeek ? content.Length : null,
            };
    }

    Task<OciBlobExistsResult?> TryCheckBlobExistsAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default);

    PackageOrigin MirrorOrigin(SurfaceContext surface);
    MirrorCachingStrategy Strategy(SurfaceContext surface);
    bool ShouldPersist(SurfaceContext surface);
}
