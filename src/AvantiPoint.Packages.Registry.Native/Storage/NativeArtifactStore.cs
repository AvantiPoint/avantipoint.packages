using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Registry.Native.Storage;

public sealed class NativeArtifactStore(IContext context, IStorageService storage, IFeedRegistry registry,
    NativeChecksumCache? checksumCache = null)
{
    private readonly IStreamingStorageService _blobs = storage as IStreamingStorageService
        ?? throw new InvalidOperationException("Native feeds require a streaming storage provider.");
    private readonly string _prefix = string.IsNullOrEmpty(registry.Feed.StoragePrefix)
        ? "native/" : registry.Feed.StoragePrefix.TrimEnd('/') + "/native/";

    private string BlobPath(string hash) => _prefix + "v2/blobs/sha256/" + hash + "/data";

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

    public async Task CopyToAsync(NativeArtifact artifact, Stream destination, CancellationToken ct)
    {
        using var bounded = new BoundedWriteStream(destination, artifact.Length, ct);
        await _blobs.CopyToAsync(BlobPath(artifact.ContentHash), bounded, ct);
        if (bounded.BytesWritten != artifact.Length) throw new IOException("Stored artifact is incomplete.");
    }

    public Task<string> GetChecksumAsync(NativeArtifact artifact, string algorithm, CancellationToken ct)
    {
        // Only catalog entries reach this method; PutAsync still reads storage to
        // verify its bytes before making an identity visible.
        if (algorithm == "sha256") return Task.FromResult(artifact.ContentHash);
        var key = _prefix + artifact.ContentHash + ":" + algorithm;
        return checksumCache?.GetAsync(key, () => ComputeHashAsync(artifact, algorithm, ct))
            ?? ComputeHashAsync(artifact, algorithm, ct);
    }

    public async Task<string> ComputeHashAsync(NativeArtifact artifact, string algorithm, CancellationToken ct)
    {
        using HashAlgorithm hash = algorithm switch
        {
            "md5" => MD5.Create(), "sha1" => SHA1.Create(), "sha256" => SHA256.Create(),
            "sha512" => SHA512.Create(), _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };
        await using var output = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write, leaveOpen: true);
        await CopyToAsync(artifact, output, ct);
        await output.FlushFinalBlockAsync(ct);
        return Convert.ToHexStringLower(hash.Hash!);
    }

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
        await _blobs.UploadAsync(BlobPath(upload.Sha256), upload.Stream, contentType, ct);
        var artifact = new NativeArtifact
        {
            FeedId = surface.FeedId, Protocol = surface.Protocol.ToString(), Path = path,
            PathHash = HashPath(path), ContentHash = upload.Sha256, Length = upload.Length,
            ContentType = contentType, PackageName = packageName, Version = version,
            MetadataJson = metadataJson, PublishedUtc = DateTime.UtcNow,
        };
        // Verify persisted bytes through the bounded streaming path before the
        // transactional identity becomes visible to consumers.
        if (await ComputeHashAsync(artifact, "sha256", ct) != upload.Sha256)
            throw new IOException("Artifact storage integrity verification failed.");
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
