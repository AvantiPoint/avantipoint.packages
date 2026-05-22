using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed class OciGarbageCollectionService
{
    private readonly IContext _context;

    public OciGarbageCollectionService(IContext context)
    {
        _context = context;
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
        if (!dryRun)
        {
            throw new NotSupportedException(
                "OCI garbage collection delete mode is not supported until manifest and layer reference "
                + "graph traversal is implemented. Use dryRun: true.");
        }

        return new OciGarbageCollectionResult(orphaned.Count, orphaned.Select(b => b.Digest).ToList(), Deleted: false);
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
