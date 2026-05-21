using System.Text;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Metrics;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed class OciRegistryService : IOciRegistryService
{
    private readonly IContext _context;
    private readonly IStorageBackendFactory _storageFactory;
    private readonly OciFeedOptionsAccessor _optionsAccessor;
    private readonly FeedMetricsService _metrics;
    private readonly ILogger<OciRegistryService> _logger;

    public OciRegistryService(
        IContext context,
        IStorageBackendFactory storageFactory,
        OciFeedOptionsAccessor optionsAccessor,
        FeedMetricsService metrics,
        ILogger<OciRegistryService> logger)
    {
        _context = context;
        _storageFactory = storageFactory;
        _optionsAccessor = optionsAccessor;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<OciManifestResult?> GetManifestAsync(
        SurfaceContext surface,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var digest = await ResolveManifestDigestAsync(scope, repositoryName, reference, cancellationToken);
        if (digest is null)
        {
            return null;
        }

        var manifest = await _context.OciManifests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.FeedId == scope.FeedId
                     && m.OciSegment == scope.OciSegment
                     && m.Digest == digest,
                cancellationToken);

        if (manifest is null)
        {
            return null;
        }

        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
        await using var stream = await store.GetAsync(algorithm, hex, cancellationToken);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        _metrics.RecordPull(surface, repositoryName);
        return new OciManifestResult(digest, manifest.MediaType, memory.ToArray());
    }

    public async Task<OciPutManifestResult> PutManifestAsync(
        SurfaceContext surface,
        string repositoryName,
        string reference,
        string mediaType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsAccessor.GetOptions(surface);
        if (!OciManifestParser.IsAllowedMediaType(mediaType, options.AllowUnknownMediaTypes))
        {
            throw new OciRegistryException("Manifest media type is not allowed.");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var parsed = OciManifestParser.Parse(mediaType, bytes);
        ValidatePlatformPolicy(options, parsed);

        buffer.Position = 0;
        var digest = await DigestBlobStore.ComputeSha256DigestAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var scope = ToScope(surface);
        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);

        if (!await store.ExistsAsync(algorithm, hex, cancellationToken))
        {
            await store.PutAsync(algorithm, hex, buffer, cancellationToken);
            await EnsureBlobRecordAsync(scope, digest, bytes.Length, cancellationToken);
        }

        foreach (var referencedDigest in parsed.ReferencedDigests)
        {
            if (!await BlobRecordExistsAsync(scope, referencedDigest, cancellationToken))
            {
                throw new OciRegistryException($"Referenced blob '{referencedDigest}' was not found.");
            }
        }

        await EnsureManifestRecordAsync(scope, digest, mediaType, parsed, bytes.Length, cancellationToken);
        await EnsureRepositoryAsync(scope, repositoryName, cancellationToken);

        await UpsertTagAsync(scope, repositoryName, reference, digest, cancellationToken);

        _metrics.RecordPush(surface, repositoryName);
        _logger.LogInformation(
            "Published OCI manifest {Digest} to {Repository} on feed {FeedId} segment {Segment}",
            digest,
            repositoryName,
            scope.FeedId,
            scope.OciSegment ?? "(default)");

        return new OciPutManifestResult(digest, mediaType);
    }

    public async Task<OciBlobResult?> GetBlobAsync(
        SurfaceContext surface,
        string digest,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var blob = await _context.OciBlobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.FeedId == scope.FeedId
                     && b.OciSegment == scope.OciSegment
                     && b.Digest == digest,
                cancellationToken);

        if (blob is null)
        {
            return null;
        }

        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
        var stream = await store.GetAsync(algorithm, hex, cancellationToken);
        _metrics.RecordPull(surface, digest);
        return new OciBlobResult(digest, stream, blob.Size);
    }

    public async Task<OciBlobExistsResult> BlobExistsAsync(
        SurfaceContext surface,
        string digest,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var blob = await _context.OciBlobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.FeedId == scope.FeedId
                     && b.OciSegment == scope.OciSegment
                     && b.Digest == digest,
                cancellationToken);

        return blob is null
            ? new OciBlobExistsResult(false, 0)
            : new OciBlobExistsResult(true, blob.Size);
    }

    public async Task<OciStartUploadResult> StartUploadAsync(
        SurfaceContext surface,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        await EnsureRepositoryAsync(scope, repositoryName, cancellationToken);

        var uploadId = Guid.NewGuid().ToString("N");
        var storagePath = $"v2/uploads/{uploadId}/data";

        _context.OciUploads.Add(new OciUpload
        {
            UploadId = uploadId,
            FeedId = scope.FeedId,
            OciSegment = scope.OciSegment,
            RepositoryName = repositoryName,
            StoragePath = storagePath,
            BytesReceived = 0,
        });
        await _context.SaveChangesAsync(cancellationToken);

        var location = BuildUploadLocation(surface, repositoryName, uploadId);
        return new OciStartUploadResult(uploadId, location);
    }

    public async Task<OciPatchUploadResult> PatchUploadAsync(
        SurfaceContext surface,
        string repositoryName,
        string uploadId,
        Stream content,
        long? start,
        long? end,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var upload = await GetUploadAsync(scope, uploadId, repositoryName, cancellationToken);
        var pathStore = GetPathStore(surface);

        using var updated = new MemoryStream();
        try
        {
            await using var existing = await pathStore.GetAsync(upload.StoragePath, cancellationToken);
            if (existing.Length > 0)
            {
                await existing.CopyToAsync(updated, cancellationToken);
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }

        var offset = start ?? updated.Length;
        if (offset > updated.Length)
        {
            updated.SetLength(offset);
        }

        updated.Position = offset;
        await content.CopyToAsync(updated, cancellationToken);
        updated.Position = 0;

        await pathStore.PutAsync(upload.StoragePath, updated, cancellationToken);
        upload.BytesReceived = updated.Length;
        await _context.SaveChangesAsync(cancellationToken);

        var rangeEnd = end ?? (updated.Length - 1);
        return new OciPatchUploadResult(uploadId, BuildUploadLocation(surface, repositoryName, uploadId), rangeEnd);
    }

    public async Task<OciCompleteUploadResult> CompleteUploadAsync(
        SurfaceContext surface,
        string repositoryName,
        string uploadId,
        string digest,
        Stream? content,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var upload = await GetUploadAsync(scope, uploadId, repositoryName, cancellationToken);
        var pathStore = GetPathStore(surface);
        var digestStore = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);

        byte[] uploadBytes;
        if (content is not null)
        {
            using var readBuffer = new MemoryStream();
            await content.CopyToAsync(readBuffer, cancellationToken);
            uploadBytes = readBuffer.ToArray();
        }
        else
        {
            await using var uploadStream = await pathStore.GetAsync(upload.StoragePath, cancellationToken);
            using var readBuffer = new MemoryStream();
            await uploadStream.CopyToAsync(readBuffer, cancellationToken);
            uploadBytes = readBuffer.ToArray();
        }

        using var buffer = new MemoryStream(uploadBytes);
        var computedDigest = await DigestBlobStore.ComputeSha256DigestAsync(buffer, cancellationToken);
        if (!string.Equals(computedDigest, digest, StringComparison.OrdinalIgnoreCase))
        {
            throw new OciRegistryException(
                $"Upload digest does not match blob content. Expected '{digest}', computed '{computedDigest}'.");
        }

        buffer.Position = 0;
        await digestStore.PutAsync(algorithm, hex, buffer, cancellationToken);
        await EnsureBlobRecordAsync(scope, digest, buffer.Length, cancellationToken);

        await pathStore.DeleteAsync(upload.StoragePath, cancellationToken);
        _context.OciUploads.Remove(upload);
        await _context.SaveChangesAsync(cancellationToken);

        _metrics.RecordPush(surface, repositoryName);
        return new OciCompleteUploadResult(digest, BuildBlobLocation(surface, repositoryName, digest));
    }

    public async Task<OciTagListResult?> ListTagsAsync(
        SurfaceContext surface,
        string repositoryName,
        int? max,
        string? last,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var options = _optionsAccessor.GetOptions(surface);
        var repository = await _context.OciRepositories
            .AsNoTracking()
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(
                r => r.FeedId == scope.FeedId
                     && r.OciSegment == scope.OciSegment
                     && r.Name == repositoryName,
                cancellationToken);

        if (repository is null)
        {
            return null;
        }

        var tags = repository.Tags
            .Where(t => IsVisibleInDiscovery(t.Origin, options) && !IsDigestReference(t.Tag))
            .Select(t => t.Tag)
            .OrderBy(t => t, StringComparer.Ordinal)
            .AsEnumerable();

        if (!string.IsNullOrEmpty(last))
        {
            tags = tags.Where(t => string.CompareOrdinal(t, last) > 0);
        }

        if (max is > 0)
        {
            tags = tags.Take(max.Value);
        }

        return new OciTagListResult(repositoryName, tags.ToList());
    }

    public async Task<OciCatalogResult> ListCatalogAsync(
        SurfaceContext surface,
        int? max,
        string? last,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var options = _optionsAccessor.GetOptions(surface);

        var repositories = await _context.OciRepositories
            .AsNoTracking()
            .Where(r => r.FeedId == scope.FeedId && r.OciSegment == scope.OciSegment)
            .Select(r => new
            {
                r.Name,
                HasVisibleTag = r.Tags.Any(t => IsVisibleInDiscovery(t.Origin, options)),
            })
            .ToListAsync(cancellationToken);

        var names = repositories
            .Where(r => r.HasVisibleTag)
            .Select(r => r.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .AsEnumerable();

        if (!string.IsNullOrEmpty(last))
        {
            names = names.Where(n => string.CompareOrdinal(n, last) > 0);
        }

        if (max is > 0)
        {
            names = names.Take(max.Value);
        }

        return new OciCatalogResult(names.ToList());
    }

    private async Task<string?> ResolveManifestDigestAsync(
        OciScope scope,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken)
    {
        var repository = await _context.OciRepositories
            .AsNoTracking()
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(
                r => r.FeedId == scope.FeedId
                     && r.OciSegment == scope.OciSegment
                     && r.Name == repositoryName,
                cancellationToken);

        if (repository is null)
        {
            return null;
        }

        if (IsDigestReference(reference))
        {
            return repository.Tags.Any(t =>
                string.Equals(t.ManifestDigest, reference, StringComparison.OrdinalIgnoreCase))
                ? reference
                : null;
        }

        return repository.Tags.FirstOrDefault(t => t.Tag == reference)?.ManifestDigest;
    }

    private async Task EnsureRepositoryAsync(OciScope scope, string repositoryName, CancellationToken cancellationToken)
    {
        var exists = await _context.OciRepositories.AnyAsync(
            r => r.FeedId == scope.FeedId
                 && r.OciSegment == scope.OciSegment
                 && r.Name == repositoryName,
            cancellationToken);

        if (exists)
        {
            return;
        }

        _context.OciRepositories.Add(new OciRepository
        {
            FeedId = scope.FeedId,
            OciSegment = scope.OciSegment,
            Name = repositoryName,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureManifestRecordAsync(
        OciScope scope,
        string digest,
        string mediaType,
        ParsedOciManifest parsed,
        long size,
        CancellationToken cancellationToken)
    {
        var exists = await _context.OciManifests.AnyAsync(
            m => m.FeedId == scope.FeedId
                 && m.OciSegment == scope.OciSegment
                 && m.Digest == digest,
            cancellationToken);

        if (exists)
        {
            return;
        }

        _context.OciManifests.Add(new OciManifest
        {
            FeedId = scope.FeedId,
            OciSegment = scope.OciSegment,
            Digest = digest,
            MediaType = mediaType,
            PlatformOs = parsed.PlatformOs,
            PlatformArch = parsed.PlatformArch,
            ArtifactKind = parsed.ArtifactKind,
            Origin = PackageOrigin.Published,
            Size = size,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBlobRecordAsync(
        OciScope scope,
        string digest,
        long size,
        CancellationToken cancellationToken)
    {
        if (await BlobRecordExistsAsync(scope, digest, cancellationToken))
        {
            return;
        }

        _context.OciBlobs.Add(new OciBlob
        {
            FeedId = scope.FeedId,
            OciSegment = scope.OciSegment,
            Digest = digest,
            Size = size,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> BlobRecordExistsAsync(OciScope scope, string digest, CancellationToken cancellationToken) =>
        _context.OciBlobs.AnyAsync(
            b => b.FeedId == scope.FeedId
                 && b.OciSegment == scope.OciSegment
                 && b.Digest == digest,
            cancellationToken);

    private async Task UpsertTagAsync(
        OciScope scope,
        string repositoryName,
        string tag,
        string digest,
        CancellationToken cancellationToken)
    {
        var repository = await _context.OciRepositories
            .Include(r => r.Tags)
            .FirstAsync(
                r => r.FeedId == scope.FeedId
                     && r.OciSegment == scope.OciSegment
                     && r.Name == repositoryName,
                cancellationToken);

        var existing = repository.Tags.FirstOrDefault(t => t.Tag == tag);
        if (existing is null)
        {
            _context.OciTags.Add(new OciTag
            {
                FeedId = scope.FeedId,
                OciSegment = scope.OciSegment,
                RepositoryKey = repository.Key,
                Tag = tag,
                ManifestDigest = digest,
                Origin = PackageOrigin.Published,
            });
        }
        else
        {
            existing.ManifestDigest = digest;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<OciUpload> GetUploadAsync(
        OciScope scope,
        string uploadId,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        var upload = await _context.OciUploads.FirstOrDefaultAsync(
            u => u.UploadId == uploadId
                 && u.FeedId == scope.FeedId
                 && u.OciSegment == scope.OciSegment
                 && u.RepositoryName == repositoryName,
            cancellationToken);

        return upload ?? throw new OciRegistryException($"Upload '{uploadId}' was not found.");
    }

    private IDigestBlobStore GetDigestStore(SurfaceContext surface) =>
        _storageFactory.CreateDigestStore(OciSurfaceOptionsBuilder.GetStorageSubPrefix(surface.OciSegment));

    private IPathBlobStore GetPathStore(SurfaceContext surface) =>
        _storageFactory.CreatePathStore(OciSurfaceOptionsBuilder.GetStorageSubPrefix(surface.OciSegment));

    private static OciScope ToScope(SurfaceContext surface) =>
        new(surface.FeedId, surface.OciSegment);

    private static bool IsDigestReference(string reference) =>
        reference.Contains(':', StringComparison.Ordinal);

    private static bool IsVisibleInDiscovery(PackageOrigin origin, OciFeedOptions options) =>
        origin switch
        {
            PackageOrigin.Published => true,
            PackageOrigin.Mirrored => options.IncludeMirroredInCatalog,
            PackageOrigin.Cached => false,
            _ => false,
        };

    private static void ValidatePlatformPolicy(OciFeedOptions options, ParsedOciManifest parsed)
    {
        if (options.Profile != OciProfile.ContainerImages
            || options.PlatformPolicy.AllowedOs.Count == 0
            || parsed.PlatformOs is null)
        {
            return;
        }

        if (!options.PlatformPolicy.AllowedOs.Contains(parsed.PlatformOs, StringComparer.OrdinalIgnoreCase))
        {
            throw new OciRegistryException(
                $"Platform OS '{parsed.PlatformOs}' is not allowed for this OCI registration.");
        }
    }

    private static string BuildUploadLocation(SurfaceContext surface, string repositoryName, string uploadId)
    {
        var baseUrl = surface.PublicBaseUrl.ToString().TrimEnd('/');
        return $"{baseUrl}/v2/{repositoryName}/blobs/uploads/{uploadId}";
    }

    private static string BuildBlobLocation(SurfaceContext surface, string repositoryName, string digest)
    {
        var baseUrl = surface.PublicBaseUrl.ToString().TrimEnd('/');
        return $"{baseUrl}/v2/{repositoryName}/blobs/{digest}";
    }
}

public sealed class OciRegistryException : Exception
{
    public OciRegistryException(string message)
        : base(message)
    {
    }
}
