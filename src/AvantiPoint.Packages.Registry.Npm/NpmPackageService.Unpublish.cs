using System.Data;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Npm;

public sealed partial class NpmPackageService
{
    public async Task UnpublishAsync(string feedId, string packageName, string revision, JsonObject? remainingPackument, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var package = await GetLocalPackageForMutationAsync(feedId, packageName, cancellationToken);
        if (package.Versions.Count == 0) throw new NpmRegistryException(404, "E404", "The package is unpublished.");
        if (revision != GetRevision(package))
            throw new NpmRegistryException(409, "E409", "Package metadata has changed. Fetch it again before unpublishing.");

        var remaining = new HashSet<string>(StringComparer.Ordinal);
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        if (remainingPackument is not null)
        {
            if (remainingPackument["_rev"] is not JsonValue rev || !rev.TryGetValue<string>(out var bodyRevision) || bodyRevision != revision
                || remainingPackument["versions"] is not JsonObject versions || versions.Count == 0 || versions.Count >= package.Versions.Count
                || versions.Any(v => !package.Versions.Any(stored => stored.Version == v.Key)))
                throw new NpmRegistryException(400, "E400", "An unpublish update must retain a strict subset of existing versions and the current revision.");
            remaining.UnionWith(versions.Select(v => v.Key));
            if (remainingPackument["dist-tags"] is not JsonObject suppliedTags)
                throw new NpmRegistryException(400, "E400", "The remaining dist-tags are required.");
            foreach (var tag in suppliedTags)
            {
                ValidateDistTag(tag.Key);
                if (tag.Value is not JsonValue value || !value.TryGetValue<string>(out var version) || !remaining.Contains(version))
                    throw new NpmRegistryException(400, "E400", "Dist-tags must reference retained versions.");
                tags.Add(tag.Key, version);
            }
        }

        foreach (var version in package.Versions.Where(v => !remaining.Contains(v.Version)))
            version.IsDeleted = true;
        foreach (var tag in package.DistTags.ToArray())
        {
            if (tags.Remove(tag.Tag, out var target)) tag.Version = target;
            else _context.NpmDistTags.Remove(tag);
        }
        foreach (var tag in tags) UpsertDistTag(package, tag.Key, tag.Value, feedId);
        // Only deletion and dist-tags are accepted; retained metadata is never overwritten by this request.
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Unpublished npm versions from {Package} in feed {FeedId}; artifact cleanup is queued", package.Name, feedId);
    }

    public async Task ConfirmTarballRemovalAsync(string feedId, string packageName, string tarballFileName, string revision, CancellationToken cancellationToken = default)
    {
        var package = await GetLocalPackageForMutationAsync(feedId, packageName, cancellationToken);
        if (revision != GetRevision(package)) throw new NpmRegistryException(409, "E409", "Package metadata has changed.");
        var path = $"{EncodePackagePath(package.Name)}/-/{tarballFileName}";
        if (!await _context.NpmVersions.IgnoreQueryFilters().AnyAsync(v => v.FeedId == feedId && v.PackageKey == package.Key
            && v.IsDeleted && v.TarballPath == path, cancellationToken))
            throw new NpmRegistryException(404, "E404", "No unpublished artifact matches this request.");
        // npm's second phase is an acknowledgement. The durable tombstone already queued cleanup.
    }

    public async Task<int> CleanupUnpublishedArtifactsAsync(string feedId, CancellationToken cancellationToken = default)
    {
        var pending = await _context.NpmVersions.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.FeedId == feedId && v.IsDeleted && !v.ArtifactDeleted)
            .OrderBy(v => v.Key).Take(20).Select(v => new { v.Key, v.TarballPath }).ToListAsync(cancellationToken);
        var cleaned = 0;
        foreach (var item in pending)
        {
            try
            {
                // Paths cannot be reused: publication checks tombstones too. Deletion is idempotent,
                // so a crash after storage deletion but before this update is safe on the next run.
                if (await _blobStore.ExistsAsync(item.TarballPath, cancellationToken))
                    await _blobStore.DeleteAsync(item.TarballPath, cancellationToken);
                await _context.NpmVersions.IgnoreQueryFilters().Where(v => v.FeedId == feedId && v.Key == item.Key && v.IsDeleted)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.ArtifactDeleted, true), cancellationToken);
                cleaned++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                _logger.LogWarning("Cleanup for unpublished npm version {VersionKey} will be retried ({ErrorType})", item.Key, exception.GetType().Name);
            }
        }
        return cleaned;
    }
}
