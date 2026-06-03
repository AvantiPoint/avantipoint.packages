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

    Task<Stream?> TryFetchBlobAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default);

    Task<OciBlobExistsResult?> TryCheckBlobExistsAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default);

    PackageOrigin MirrorOrigin(SurfaceContext surface);
    MirrorCachingStrategy Strategy(SurfaceContext surface);
    bool ShouldPersist(SurfaceContext surface);
}
