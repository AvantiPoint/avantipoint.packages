using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Metrics;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci;

/// <summary>
/// Reclaims OCI content that is no longer reachable from any repository tag. Reachability is
/// calculated through the complete manifest graph before any content is deleted.
/// </summary>
public sealed class OciGarbageCollectionService(
    IContext context,
    IStorageBackendFactory storageFactory,
    FeedMetricsService metrics,
    TimeProvider timeProvider,
    ILogger<OciGarbageCollectionService> logger)
{
    public async Task<OciGarbageCollectionResult> CollectAsync(
        SurfaceContext surface,
        bool dryRun = true,
        TimeSpan? minimumAge = null,
        CancellationToken cancellationToken = default) =>
        await CollectAsync(
            new OciScope(surface.FeedId, surface.OciSegment),
            dryRun,
            minimumAge,
            cancellationToken);

    internal async Task<OciGarbageCollectionResult> CollectAsync(
        OciScope scope,
        bool dryRun,
        TimeSpan? minimumAge,
        CancellationToken cancellationToken)
    {
        var retentionAge = minimumAge ?? TimeSpan.FromHours(24);
        if (retentionAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAge), "Minimum age cannot be negative.");
        }

        var blobs = await context.OciBlobs
            .Where(blob => blob.FeedId == scope.FeedId && blob.OciSegment == scope.OciSegment)
            .ToListAsync(cancellationToken);
        var manifests = await context.OciManifests
            .Where(manifest => manifest.FeedId == scope.FeedId && manifest.OciSegment == scope.OciSegment)
            .ToListAsync(cancellationToken);
        var roots = await context.OciTags
            .Where(tag => tag.FeedId == scope.FeedId && tag.OciSegment == scope.OciSegment)
            .Select(tag => tag.ManifestDigest)
            .Distinct()
            .ToListAsync(cancellationToken);

        var store = storageFactory.CreateDigestStore(
            OciSurfaceOptionsBuilder.GetStorageSubPrefix(scope.OciSegment));
        var reachable = await FindReachableDigestsAsync(
            roots,
            manifests,
            store,
            cancellationToken);
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - retentionAge;
        var orphaned = blobs
            .Where(blob => blob.CreatedAt <= cutoff && !reachable.Contains(blob.Digest))
            .OrderBy(blob => blob.Digest, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var bytes = orphaned.Sum(blob => blob.Size);

        logger.LogInformation(
            "OCI garbage collection found {BlobCount} orphaned blobs totaling {BlobBytes} bytes on feed {FeedId} segment {Segment}; DryRun={DryRun}",
            orphaned.Count,
            bytes,
            scope.FeedId,
            scope.OciSegment ?? "(default)",
            dryRun);

        if (!dryRun)
        {
            var manifestsByDigest = manifests
                .GroupBy(manifest => manifest.Digest, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var blob in orphaned)
            {
                var (algorithm, hex) = DigestBlobStore.ParseDigest(blob.Digest);
                if (await store.ExistsAsync(algorithm, hex, cancellationToken))
                {
                    await store.DeleteAsync(algorithm, hex, cancellationToken);
                }

                if (manifestsByDigest.TryGetValue(blob.Digest, out var orphanedManifests))
                {
                    context.OciManifests.RemoveRange(orphanedManifests);
                }

                context.OciBlobs.Remove(blob);
                await context.SaveChangesAsync(cancellationToken);
                metrics.RecordBlobBytes(scope.FeedId, FeedProtocol.Oci, -blob.Size);
            }
        }

        return new OciGarbageCollectionResult(
            orphaned.Count,
            orphaned.Select(blob => blob.Digest).ToList(),
            Deleted: !dryRun)
        {
            Bytes = bytes,
        };
    }

    private static async Task<HashSet<string>> FindReachableDigestsAsync(
        IEnumerable<string> roots,
        IReadOnlyCollection<OciManifest> manifests,
        IDigestBlobStore store,
        CancellationToken cancellationToken)
    {
        var manifestsByDigest = manifests
            .GroupBy(manifest => manifest.Digest, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        foreach (var root in roots)
        {
            if (reachable.Add(root))
            {
                pending.Enqueue(root);
            }
        }

        while (pending.TryDequeue(out var digest))
        {
            if (!manifestsByDigest.TryGetValue(digest, out var manifest))
            {
                continue;
            }

            var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
            await using var stream = await store.GetAsync(algorithm, hex, cancellationToken);
            using var content = new MemoryStream();
            await stream.CopyToAsync(content, cancellationToken);
            var parsed = OciManifestParser.Parse(manifest.MediaType, content.ToArray());
            foreach (var referencedDigest in parsed.ReferencedDigests)
            {
                if (reachable.Add(referencedDigest))
                {
                    pending.Enqueue(referencedDigest);
                }
            }
        }

        return reachable;
    }
}

public sealed record OciGarbageCollectionResult(
    int Count,
    IReadOnlyList<string> Digests,
    bool Deleted)
{
    public long Bytes { get; init; }
}
