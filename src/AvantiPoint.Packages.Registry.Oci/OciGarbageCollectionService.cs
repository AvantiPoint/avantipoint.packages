using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed class OciGarbageCollectionService
{
    private readonly IContext _context;
    private readonly IStorageBackendFactory _storageFactory;
    private readonly ILogger<OciGarbageCollectionService> _logger;

    public OciGarbageCollectionService(
        IContext context,
        IStorageBackendFactory storageFactory,
        ILogger<OciGarbageCollectionService> logger)
    {
        _context = context;
        _storageFactory = storageFactory;
        _logger = logger;
    }

    public async Task<OciGarbageCollectionResult> CollectAsync(
        SurfaceContext surface,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var scope = new OciScope(surface.FeedId, surface.OciSegment);
        var referencedDigests = await GetReferencedDigestsAsync(scope, cancellationToken);
        var blobs = await _context.OciBlobs
            .Where(b => b.FeedId == scope.FeedId && b.OciSegment == scope.OciSegment)
            .ToListAsync(cancellationToken);

        var orphaned = blobs.Where(b => !referencedDigests.Contains(b.Digest)).ToList();
        if (dryRun)
        {
            return new OciGarbageCollectionResult(orphaned.Count, orphaned.Select(b => b.Digest).ToList(), Deleted: false);
        }

        var store = _storageFactory.CreateDigestStore(
            OciSurfaceOptionsBuilder.GetStorageSubPrefix(surface.OciSegment));

        foreach (var blob in orphaned)
        {
            var (algorithm, hex) = DigestBlobStore.ParseDigest(blob.Digest);
            await store.DeleteAsync(algorithm, hex, cancellationToken);
            _context.OciBlobs.Remove(blob);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "OCI GC reclaimed {Count} blobs for feed {FeedId} segment {Segment}",
            orphaned.Count,
            scope.FeedId,
            scope.OciSegment ?? "(default)");

        return new OciGarbageCollectionResult(orphaned.Count, orphaned.Select(b => b.Digest).ToList(), Deleted: true);
    }

    private async Task<HashSet<string>> GetReferencedDigestsAsync(OciScope scope, CancellationToken cancellationToken)
    {
        var tagDigests = await _context.OciTags
            .Where(t => t.FeedId == scope.FeedId && t.OciSegment == scope.OciSegment)
            .Select(t => t.ManifestDigest)
            .ToListAsync(cancellationToken);

        return tagDigests.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record OciGarbageCollectionResult(int Count, IReadOnlyList<string> Digests, bool Deleted);
