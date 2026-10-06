using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Registry.Native.Storage;

public sealed class NativeArtifactStore(IContext context, IStorageBackendFactory storage)
{
    private readonly IDigestBlobStore _blobs = storage.CreateDigestStore("native/");

    public async Task<NativeArtifact?> FindAsync(SurfaceContext surface, string path, CancellationToken ct)
    {
        if (!ArtifactPath.IsValid(path)) return null;
        var key = HashPath(path);
        var protocol = surface.Protocol.ToString();
        var artifact = await context.NativeArtifacts.AsNoTracking().SingleOrDefaultAsync(
            a => a.FeedId == surface.FeedId && a.Protocol == protocol && a.PathHash == key, ct);
        // Never alias different paths, even in the event of a hash collision.
        return artifact?.Path == path && artifact.FeedId == surface.FeedId && artifact.Protocol == protocol ? artifact : null;
    }

    public async Task<IReadOnlyList<NativeArtifact>> ListAsync(
        SurfaceContext surface, string packageName, CancellationToken ct)
    {
        var protocol = surface.Protocol.ToString();
        var artifacts = await context.NativeArtifacts.AsNoTracking().Where(
            a => a.FeedId == surface.FeedId && a.Protocol == protocol && a.PackageName == packageName)
            .ToListAsync(ct);
        // Provider-default collations may be case-insensitive; Maven and artifact
        // identities are exact, so never disclose neighboring case variants.
        return artifacts.Where(a => a.FeedId == surface.FeedId && a.Protocol == protocol
            && a.PackageName == packageName).ToArray();
    }

    public Task<Stream> OpenAsync(NativeArtifact artifact, CancellationToken ct) =>
        _blobs.GetAsync("sha256", artifact.ContentHash, ct);

    public async Task<StoragePutResult> PutAsync(
        SurfaceContext surface, string path, string packageName, string? version,
        string contentType, ArtifactUpload upload, string? metadataJson, CancellationToken ct)
    {
        if (!ArtifactPath.IsValid(path)) throw new ArgumentException("Invalid artifact path.", nameof(path));
        if (packageName.Length > 256 || version?.Length > 128) throw new ArgumentException("Invalid identity.");
        var existing = await FindAsync(surface, path, ct);
        if (existing is not null)
            return existing.ContentHash == upload.Sha256 ? StoragePutResult.AlreadyExists : StoragePutResult.Conflict;

        // Objects are addressed by their bytes. The database's unique constraint is
        // the cross-process publication gate, even for providers with mutable PUT.
        upload.Stream.Position = 0;
        await _blobs.PutAsync("sha256", upload.Sha256, upload.Stream, ct);
        // Some legacy stores suppress put conflicts or can retain partial bytes
        // after interruption. Never commit an identity until persisted bytes verify.
        await using (var stored = await _blobs.GetAsync("sha256", upload.Sha256, ct))
        {
            if (stored is null || Convert.ToHexStringLower(await SHA256.HashDataAsync(stored, ct)) != upload.Sha256)
                throw new IOException("Artifact storage integrity verification failed.");
        }
        var artifact = new NativeArtifact
        {
            FeedId = surface.FeedId, Protocol = surface.Protocol.ToString(), Path = path,
            PathHash = HashPath(path), ContentHash = upload.Sha256, Length = upload.Length,
            ContentType = contentType, PackageName = packageName, Version = version,
            MetadataJson = metadataJson, PublishedUtc = DateTime.UtcNow,
        };
        context.NativeArtifacts.Add(artifact);
        try
        {
            await context.SaveChangesAsync(ct);
            return StoragePutResult.Success;
        }
        catch (DbUpdateException ex) when (context.IsUniqueConstraintViolationException(ex))
        {
            context.NativeArtifacts.Entry(artifact).State = EntityState.Detached;
            existing = await FindAsync(surface, path, ct);
            if (existing is null) throw;
            return existing.ContentHash == upload.Sha256 ? StoragePutResult.AlreadyExists : StoragePutResult.Conflict;
        }
    }

    private static string HashPath(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path)));
}
