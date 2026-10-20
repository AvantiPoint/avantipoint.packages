using AvantiPoint.Feed.Platform;

namespace AvantiPoint.Packages.Registry.Oci;

public interface IOciRegistryService
{
    Task<OciManifestResult?> GetManifestAsync(
        SurfaceContext surface,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken = default);

    Task<OciPutManifestResult> PutManifestAsync(
        SurfaceContext surface,
        string repositoryName,
        string reference,
        string mediaType,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<OciBlobResult?> GetBlobAsync(
        SurfaceContext surface,
        string digest,
        string? repositoryName = null,
        CancellationToken cancellationToken = default);

    Task<OciBlobExistsResult> BlobExistsAsync(
        SurfaceContext surface,
        string digest,
        string? repositoryName = null,
        CancellationToken cancellationToken = default);

    Task<OciStartUploadResult> StartUploadAsync(
        SurfaceContext surface,
        string repositoryName,
        CancellationToken cancellationToken = default);

    Task<OciPatchUploadResult> GetUploadStatusAsync(SurfaceContext surface, string repositoryName, string uploadId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Upload status is not implemented by this registry service.");

    Task CancelUploadAsync(SurfaceContext surface, string repositoryName, string uploadId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Upload cancellation is not implemented by this registry service.");

    Task<OciPatchUploadResult> PatchUploadAsync(
        SurfaceContext surface,
        string repositoryName,
        string uploadId,
        Stream content,
        long? start,
        long? end,
        CancellationToken cancellationToken = default);

    Task<OciCompleteUploadResult> CompleteUploadAsync(
        SurfaceContext surface,
        string repositoryName,
        string uploadId,
        string digest,
        Stream? content,
        CancellationToken cancellationToken = default);

    Task<OciCompleteUploadResult> CompleteRangedUploadAsync(SurfaceContext surface, string repositoryName, string uploadId,
        string digest, Stream? content, long? start, long? end, CancellationToken cancellationToken = default) =>
        start.HasValue || end.HasValue
            ? throw new NotSupportedException("Ranged upload completion is not implemented by this registry service.")
            : CompleteUploadAsync(surface, repositoryName, uploadId, digest, content, cancellationToken);

    Task<OciTagListResult?> ListTagsAsync(
        SurfaceContext surface,
        string repositoryName,
        int? max,
        string? last,
        CancellationToken cancellationToken = default);

    Task<OciCatalogResult> ListCatalogAsync(
        SurfaceContext surface,
        int? max,
        string? last,
        CancellationToken cancellationToken = default);

    Task<OciReferrersResult> ListReferrersAsync(
        SurfaceContext surface,
        string repositoryName,
        string subjectDigest,
        string? artifactType = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new OciReferrersResult([], RepositoryFound: false));
}

public sealed record OciManifestResult(string Digest, string MediaType, byte[] Content);

public sealed record OciPutManifestResult(string Digest, string MediaType, string? SubjectDigest = null);

public sealed record OciBlobResult(string Digest, Stream Content, long? Size);

public sealed record OciBlobExistsResult(bool Exists, long Size);

public sealed record OciStartUploadResult(string UploadId, string Location);

public sealed record OciPatchUploadResult(string UploadId, string Location, long RangeEnd);

public sealed record OciCompleteUploadResult(string Digest, string Location);

public sealed record OciTagListResult(string Name, IReadOnlyList<string> Tags, bool HasMore = false);

public sealed record OciCatalogResult(IReadOnlyList<string> Repositories, bool HasMore = false);

public sealed record OciReferrersResult(
    IReadOnlyList<OciReferrerDescriptor> Manifests,
    bool RepositoryFound = true);
