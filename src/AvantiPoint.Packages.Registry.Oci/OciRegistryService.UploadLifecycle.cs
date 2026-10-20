using AvantiPoint.Feed.Platform;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed partial class OciRegistryService
{
    public async Task<OciPatchUploadResult> GetUploadStatusAsync(SurfaceContext surface, string repositoryName, string uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await GetUploadAsync(ToScope(surface), uploadId, repositoryName, cancellationToken);
        return new OciPatchUploadResult(uploadId, BuildUploadLocation(surface, repositoryName, uploadId), Math.Max(0, upload.BytesReceived - 1));
    }

    public async Task CancelUploadAsync(SurfaceContext surface, string repositoryName, string uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await GetUploadAsync(ToScope(surface), uploadId, repositoryName, cancellationToken);
        // Keep a durable cleanup record before removing storage. A failed deletion is retried.
        upload.IsCancelled = true;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
