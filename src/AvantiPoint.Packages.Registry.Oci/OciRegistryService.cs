using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Metrics;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed partial class OciRegistryService : IOciRegistryService
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastMetadataRefresh = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MetadataRefreshLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ReferrerIndexLocks = new();

    private readonly IContext _context;
    private readonly IStorageBackendFactory _storageFactory;
    private readonly OciFeedOptionsAccessor _optionsAccessor;
    private readonly FeedMetricsService _metrics;
    private readonly IOciMirrorService _mirror;
    private readonly ILogger<OciRegistryService> _logger;

    public OciRegistryService(
        IContext context,
        IStorageBackendFactory storageFactory,
        OciFeedOptionsAccessor optionsAccessor,
        FeedMetricsService metrics,
        IOciMirrorService mirror,
        ILogger<OciRegistryService> logger)
    {
        _context = context;
        _storageFactory = storageFactory;
        _optionsAccessor = optionsAccessor;
        _metrics = metrics;
        _mirror = mirror;
        _logger = logger;
    }

    public async Task<OciManifestResult?> GetManifestAsync(
        SurfaceContext surface,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var resolved = await ResolveManifestReferenceAsync(scope, repositoryName, reference, cancellationToken);
        if (resolved is null)
        {
            return await TryFetchUpstreamManifestAsync(surface, scope, repositoryName, reference, cancellationToken);
        }

        if (!IsDigestReference(reference)
            && resolved.Origin != PackageOrigin.Published)
        {
            var refreshed = await TryRefreshTrackedManifestIfDueAsync(
                surface,
                scope,
                repositoryName,
                reference,
                cancellationToken);
            if (refreshed is not null)
            {
                return refreshed;
            }

            resolved = await ResolveManifestReferenceAsync(scope, repositoryName, reference, cancellationToken)
                ?? resolved;
        }

        var manifest = await _context.OciManifests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.FeedId == scope.FeedId
                     && m.OciSegment == scope.OciSegment
                     && m.Digest == resolved.Digest,
                cancellationToken);

        if (manifest is null)
        {
            return await TryFetchUpstreamManifestAsync(surface, scope, repositoryName, reference, cancellationToken);
        }

        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(resolved.Digest);
        if (!await store.ExistsAsync(algorithm, hex, cancellationToken))
        {
            return await TryFetchUpstreamManifestAsync(surface, scope, repositoryName, reference, cancellationToken);
        }
        await using var stream = await store.GetAsync(algorithm, hex, cancellationToken);
        if (stream is null || ReferenceEquals(stream, Stream.Null))
        {
            return null;
        }
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        return new OciManifestResult(resolved.Digest, manifest.MediaType, memory.ToArray());
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

        using var buffer = await ReadManifestContentAsync(content, cancellationToken);
        var bytes = buffer.ToArray();

        var parsed = OciManifestParser.Parse(mediaType, bytes);
        ValidatePlatformPolicy(options, parsed);

        var scope = ToScope(surface);
        await using var mutation = await OciMutationTransaction.BeginAsync(_context, scope, cancellationToken);
        // A subject is a weak association, not a required content dependency. OCI
        // Distribution requires accepting referrers before their subject arrives.
        // The index and artifact membership below remain scoped to this repository;
        // recording the association never imports or grants access to the subject.

        buffer.Position = 0;
        var digest = await DigestBlobStore.ComputeSha256DigestAsync(buffer, cancellationToken);
        buffer.Position = 0;
        if (IsDigestReference(reference) && !string.Equals(reference, digest, StringComparison.OrdinalIgnoreCase))
            throw new OciRegistryException("Manifest digest does not match the supplied content.", 400, "DIGEST_INVALID");

        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);

        foreach (var referencedDigest in parsed.ReferencedDigests)
        {
            var (referencedAlgorithm, referencedHex) = DigestBlobStore.ParseDigest(referencedDigest);
            if (!await BlobRecordExistsAsync(scope, referencedDigest, cancellationToken)
                || !await store.ExistsAsync(referencedAlgorithm, referencedHex, cancellationToken))
            {
                throw new OciRegistryException($"Referenced blob '{referencedDigest}' was not found.");
            }
        }

        if (!await store.ExistsAsync(algorithm, hex, cancellationToken))
        {
            await store.PutAsync(algorithm, hex, buffer, cancellationToken);
            _metrics.RecordBlobBytes(surface, bytes.Length);
        }
        await EnsureBlobRecordAsync(scope, digest, bytes.Length, cancellationToken);
        await EnsureManifestRecordAsync(scope, digest, parsed.MediaType, parsed, bytes.Length, PackageOrigin.Published, cancellationToken);
        await EnsureRepositoryAsync(scope, repositoryName, cancellationToken);

        await UpsertTagAsync(scope, repositoryName, reference, digest, PackageOrigin.Published, cancellationToken);
        if (parsed.Subject is not null)
        {
            // Keep a hidden repository association for digest pulls even when a client used
            // a mutable tag, then atomically maintain the OCI referrers compatibility index.
            await UpsertTagAsync(scope, repositoryName, digest, digest, PackageOrigin.Published, cancellationToken);
            await UpdateReferrersIndexAsync(
                surface,
                scope,
                repositoryName,
                parsed.Subject.Digest,
                new OciReferrerDescriptor(
                    parsed.MediaType,
                    digest,
                    bytes.Length,
                    parsed.ArtifactType,
                    parsed.Annotations),
                PackageOrigin.Published,
                cancellationToken);
        }

        await mutation.CommitAsync(cancellationToken);
        _logger.LogInformation(
            "Published OCI manifest {Digest} to {Repository} on feed {FeedId} segment {Segment}",
            digest,
            repositoryName,
            scope.FeedId,
            scope.OciSegment ?? "(default)");

        return new OciPutManifestResult(digest, parsed.MediaType, parsed.Subject?.Digest);
    }

    public async Task<OciBlobResult?> GetBlobAsync(
        SurfaceContext surface,
        string digest,
        string? repositoryName = null,
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

        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
        if (await store.ExistsAsync(algorithm, hex, cancellationToken))
        {
            var stream = await store.GetAsync(algorithm, hex, cancellationToken);
            var size = blob?.Size ?? TryGetStreamLength(stream);
            return new OciBlobResult(digest, stream, size);
        }

        if (string.IsNullOrEmpty(repositoryName))
        {
            return null;
        }

        var upstream = await _mirror.TryFetchBlobWithPolicyAsync(surface, repositoryName, digest, cancellationToken);
        if (upstream is null)
        {
            return null;
        }

        var upstreamStream = upstream.Content;
        if (upstream.CachingStrategy != MirrorCachingStrategy.ProxyOnly)
        {
            var pathStore = GetPathStore(surface);
            var stagingPath = BuildStagingPath("mirror");
            long size;
            try
            {
                await using (upstreamStream)
                await using (var tracked = new Sha256TrackingReadStream(upstreamStream, leaveOpen: true))
                {
                    await pathStore.PutAsync(stagingPath, tracked, cancellationToken);
                    EnsureStreamWasConsumed(tracked, "upstream blob");
                    size = tracked.BytesRead;
                    var actualDigest = tracked.GetDigest();
                    if (!string.Equals(actualDigest, digest, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Rejected mirrored OCI blob {Digest} from {RepositoryName}; upstream bytes hashed to {ActualDigest}",
                            digest,
                            repositoryName,
                            actualDigest);
                        return null;
                    }
                }

                await using var mutation = await OciMutationTransaction.BeginAsync(_context, scope, cancellationToken);
                await using var staged = await pathStore.GetAsync(stagingPath, cancellationToken);
                await store.PutAsync(algorithm, hex, staged, cancellationToken);
                await EnsureBlobRecordAsync(scope, digest, size, cancellationToken);
                await mutation.CommitAsync(cancellationToken);
                _metrics.RecordBlobBytes(surface, size);
            }
            finally
            {
                await TryDeletePathAsync(pathStore, stagingPath);
            }

            if (!await store.ExistsAsync(algorithm, hex, cancellationToken))
            {
                return null;
            }

            var stream = await store.GetAsync(algorithm, hex, cancellationToken);

            var persisted = await _context.OciBlobs.AsNoTracking().FirstOrDefaultAsync(
                b => b.FeedId == scope.FeedId && b.OciSegment == scope.OciSegment && b.Digest == digest,
                cancellationToken);
            return new OciBlobResult(digest, stream, persisted?.Size ?? TryGetStreamLength(stream));
        }

        return new OciBlobResult(
            digest,
            upstreamStream,
            upstream.Size ?? TryGetStreamLength(upstreamStream));
    }

    public async Task<OciBlobExistsResult> BlobExistsAsync(
        SurfaceContext surface,
        string digest,
        string? repositoryName = null,
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

        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
        if (await store.ExistsAsync(algorithm, hex, cancellationToken))
        {
            return new OciBlobExistsResult(true, blob?.Size ?? 0);
        }

        if (string.IsNullOrEmpty(repositoryName))
        {
            return new OciBlobExistsResult(false, 0);
        }

        var upstream = await _mirror.TryCheckBlobExistsAsync(surface, repositoryName, digest, cancellationToken);
        return upstream ?? new OciBlobExistsResult(false, 0);
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
        if (start.HasValue != end.HasValue || start.HasValue && (start != upload.BytesReceived || end < start))
            throw new OciRegistryException("The chunk must start at the next upload byte.", 416, "BLOB_UPLOAD_INVALID", Math.Max(0, upload.BytesReceived - 1));
        var previousPath = upload.StoragePath;
        var replacementPath = BuildStagingPath(uploadId);
        long storedLength;

        try
        {
            await using var existing = await OpenPathOrEmptyAsync(pathStore, previousPath, cancellationToken);
            await using var updated = new OverlayReadStream(
                existing,
                content,
                start,
                leaveOpen: true);
            await pathStore.PutAsync(replacementPath, updated, cancellationToken);
            EnsureStreamWasConsumed(updated, "upload chunk");
            storedLength = updated.BytesRead;
            if (end.HasValue && end.Value - start!.Value != storedLength - upload.BytesReceived - 1)
                throw new OciRegistryException("The chunk length does not match Content-Range.", 416, "BLOB_UPLOAD_INVALID", Math.Max(0, upload.BytesReceived - 1));

            upload.StoragePath = replacementPath;
            upload.BytesReceived = storedLength;
            upload.LastActivityAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeletePathAsync(pathStore, replacementPath);
            throw;
        }

        if (!string.Equals(previousPath, replacementPath, StringComparison.Ordinal))
        {
            await TryDeletePathAsync(pathStore, previousPath);
        }

        var rangeEnd = Math.Max(0, storedLength - 1);
        return new OciPatchUploadResult(uploadId, BuildUploadLocation(surface, repositoryName, uploadId), rangeEnd);
    }

    public Task<OciCompleteUploadResult> CompleteUploadAsync(
        SurfaceContext surface,
        string repositoryName,
        string uploadId,
        string digest,
        Stream? content,
        CancellationToken cancellationToken = default) =>
        CompleteRangedUploadAsync(surface, repositoryName, uploadId, digest, content, null, null, cancellationToken);

    public async Task<OciCompleteUploadResult> CompleteRangedUploadAsync(SurfaceContext surface, string repositoryName, string uploadId,
        string digest, Stream? content, long? start, long? end, CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var upload = await GetUploadAsync(scope, uploadId, repositoryName, cancellationToken);
        if (start.HasValue != end.HasValue || start.HasValue && (start != upload.BytesReceived || end < start))
            throw new OciRegistryException("The final chunk must start at the next upload byte.", 416, "BLOB_UPLOAD_INVALID", Math.Max(0, upload.BytesReceived - 1));
        var pathStore = GetPathStore(surface);
        var digestStore = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
        var stagingPath = BuildStagingPath($"{uploadId}-complete");
        long uploadedLength;

        try
        {
            await using var existing = await OpenPathOrEmptyAsync(
                pathStore,
                upload.StoragePath,
                cancellationToken);
            await using var combined = new OverlayReadStream(
                existing,
                content ?? Stream.Null,
                replacementOffset: null,
                leaveOpen: true);
            await using var tracked = new Sha256TrackingReadStream(combined, leaveOpen: true);
            await pathStore.PutAsync(stagingPath, tracked, cancellationToken);
            EnsureStreamWasConsumed(tracked, "completed upload");
            uploadedLength = tracked.BytesRead;
            if (end.HasValue && end.Value - start!.Value != uploadedLength - upload.BytesReceived - 1)
                throw new OciRegistryException("The final chunk length does not match Content-Range.", 416, "BLOB_UPLOAD_INVALID", Math.Max(0, upload.BytesReceived - 1));
            var computedDigest = tracked.GetDigest();

            if (!string.Equals(computedDigest, digest, StringComparison.OrdinalIgnoreCase))
            {
                throw new OciRegistryException(
                    $"Upload digest does not match blob content. Expected '{digest}', computed '{computedDigest}'.");
            }

            await using var mutation = await OciMutationTransaction.BeginAsync(_context, scope, cancellationToken);
            var wasStored = await digestStore.ExistsAsync(algorithm, hex, cancellationToken);
            if (!wasStored)
            {
                await using var staged = await pathStore.GetAsync(stagingPath, cancellationToken);
                await digestStore.PutAsync(algorithm, hex, staged, cancellationToken);
            }

            await EnsureBlobRecordAsync(scope, digest, uploadedLength, cancellationToken);
            if (!wasStored)
            {
                _metrics.RecordBlobBytes(surface, uploadedLength);
            }

            _context.OciUploads.Remove(upload);
            await _context.SaveChangesAsync(cancellationToken);
            await mutation.CommitAsync(cancellationToken);
        }
        finally
        {
            await TryDeletePathAsync(pathStore, stagingPath);
        }

        await TryDeletePathAsync(pathStore, upload.StoragePath);

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
            .FirstOrDefaultAsync(
                r => r.FeedId == scope.FeedId
                     && r.OciSegment == scope.OciSegment
                     && r.Name == repositoryName,
                cancellationToken);

        if (repository is null)
        {
            return null;
        }

        var pageSize = PageSize(max);
        if (pageSize == 0) return new OciTagListResult(repositoryName, []);
        if (options.Profile == OciProfile.HelmCharts && options.Mirror.RefreshTrackedArtifacts)
        {
            repository.Tags = await _context.OciTags.AsNoTracking()
                .Where(tag => tag.FeedId == scope.FeedId && tag.OciSegment == scope.OciSegment && tag.RepositoryKey == repository.Key)
                .Select(tag => new OciTag { Tag = tag.Tag, Origin = tag.Origin })
                .ToListAsync(cancellationToken);
        }

        var transientUpstreamTags = await TryRefreshTrackedHelmRepositoryIfDueAsync(
            surface,
            scope,
            repository,
            options,
            cancellationToken);

        var localTags = await ReadPageAsync(DiscoverableTags(scope, options)
            .Where(tag => tag.RepositoryKey == repository.Key).Select(tag => tag.Tag), pageSize + 1, last, cancellationToken);
        var tags = localTags
            .Concat(transientUpstreamTags ?? [])
            .Where(tag => !IsDigestReference(tag) && !OciReferrersIndex.IsInternalTag(tag)
                          && (string.IsNullOrEmpty(last) || string.CompareOrdinal(tag, last) > 0))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .Take(pageSize + 1).ToList();
        return new OciTagListResult(repositoryName, tags.Take(pageSize).ToList(), tags.Count > pageSize);
    }

    public async Task<OciReferrersResult> ListReferrersAsync(
        SurfaceContext surface,
        string repositoryName,
        string subjectDigest,
        string? artifactType = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var repositoryExists = await _context.OciRepositories
            .AsNoTracking()
            .AnyAsync(
                repository => repository.FeedId == scope.FeedId
                              && repository.OciSegment == scope.OciSegment
                              && repository.Name == repositoryName,
                cancellationToken);
        if (!repositoryExists)
        {
            return new OciReferrersResult([], RepositoryFound: false);
        }

        var referrersTag = OciReferrersIndex.BuildTag(subjectDigest);
        var index = await ReadStoredManifestAsync(
            surface,
            scope,
            repositoryName,
            referrersTag,
            cancellationToken);
        if (index is null)
        {
            return new OciReferrersResult([]);
        }

        IReadOnlyList<OciReferrerDescriptor> indexedDescriptors;
        try
        {
            indexedDescriptors = OciReferrersIndex.Parse(index.Content);
        }
        catch (JsonException)
        {
            return new OciReferrersResult([]);
        }

        var result = new List<OciReferrerDescriptor>();
        foreach (var indexed in indexedDescriptors)
        {
            var manifest = await ReadStoredManifestAsync(
                surface,
                scope,
                repositoryName,
                indexed.Digest,
                cancellationToken);
            if (manifest is null)
            {
                continue;
            }

            try
            {
                var parsed = OciManifestParser.Parse(manifest.MediaType, manifest.Content);
                if (!string.Equals(parsed.Subject?.Digest, subjectDigest, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(artifactType)
                    && !string.Equals(parsed.ArtifactType, artifactType, StringComparison.Ordinal))
                {
                    continue;
                }

                result.Add(new OciReferrerDescriptor(
                    manifest.MediaType,
                    manifest.Digest,
                    manifest.Content.LongLength,
                    parsed.ArtifactType,
                    parsed.Annotations));
            }
            catch (JsonException)
            {
                // Ignore a corrupt compatibility-index entry rather than returning an
                // invalid descriptor from the native OCI 1.1 endpoint.
            }
        }

        return new OciReferrersResult(result);
    }

    public async Task<OciCatalogResult> ListCatalogAsync(
        SurfaceContext surface,
        int? max,
        string? last,
        CancellationToken cancellationToken = default)
    {
        var scope = ToScope(surface);
        var options = _optionsAccessor.GetOptions(surface);

        var pageSize = PageSize(max);
        if (pageSize == 0) return new OciCatalogResult([]);
        var visibleTags = DiscoverableTags(scope, options);
        var repositories = _context.OciRepositories
            .AsNoTracking()
            .Where(r => r.FeedId == scope.FeedId && r.OciSegment == scope.OciSegment
                        && visibleTags.Any(tag => tag.RepositoryKey == r.Key))
            .Select(r => r.Name);
        var names = await ReadPageAsync(repositories, pageSize + 1, last, cancellationToken);
        return new OciCatalogResult(names.Take(pageSize).ToList(), names.Count > pageSize);
    }

    private async Task<OciManifestResult?> TryFetchUpstreamManifestAsync(
        SurfaceContext surface,
        OciScope scope,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken)
    {
        var upstream = await _mirror.TryFetchManifestAsync(surface, repositoryName, reference, cancellationToken);
        if (upstream is null)
        {
            return null;
        }

        var cachingStrategy = upstream.CachingStrategy ?? _mirror.Strategy(surface);
        if (cachingStrategy != MirrorCachingStrategy.ProxyOnly)
        {
            await PersistMirroredManifestAsync(
                surface,
                scope,
                repositoryName,
                reference,
                upstream,
                cachingStrategy,
                cancellationToken);
            MarkMetadataRefreshed(surface, "manifest", repositoryName, reference);
        }

        return new OciManifestResult(upstream.Digest, upstream.MediaType, upstream.Content);
    }

    private async Task<OciManifestResult?> TryRefreshTrackedManifestIfDueAsync(
        SurfaceContext surface,
        OciScope scope,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken)
    {
        var options = _optionsAccessor.GetOptions(surface);
        if (!options.Mirror.RefreshTrackedArtifacts)
        {
            return null;
        }

        var refreshKey = GetMetadataRefreshKey(surface, "manifest", repositoryName, reference);
        if (!IsMetadataRefreshDue(refreshKey, options.Mirror.MetadataRefreshInterval))
        {
            return null;
        }

        var refreshLock = MetadataRefreshLocks.GetOrAdd(refreshKey, static _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsMetadataRefreshDue(refreshKey, options.Mirror.MetadataRefreshInterval))
            {
                return null;
            }

            var upstream = await _mirror.TryFetchManifestAsync(
                surface,
                repositoryName,
                reference,
                cancellationToken);
            LastMetadataRefresh[refreshKey] = DateTimeOffset.UtcNow;
            if (upstream is null)
            {
                return null;
            }

            var cachingStrategy = upstream.CachingStrategy ?? _mirror.Strategy(surface);
            if (cachingStrategy != MirrorCachingStrategy.ProxyOnly)
            {
                await PersistMirroredManifestAsync(
                    surface,
                    scope,
                    repositoryName,
                    reference,
                    upstream,
                    cachingStrategy,
                    cancellationToken);
            }

            return new OciManifestResult(upstream.Digest, upstream.MediaType, upstream.Content);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task<IReadOnlyList<string>?> TryRefreshTrackedHelmRepositoryIfDueAsync(
        SurfaceContext surface,
        OciScope scope,
        OciRepository repository,
        OciFeedOptions options,
        CancellationToken cancellationToken)
    {
        if (options.Profile != OciProfile.HelmCharts
            || !options.Mirror.RefreshTrackedArtifacts
            || !repository.Tags.Any(static tag => tag.Origin != PackageOrigin.Published))
        {
            return null;
        }

        var refreshKey = GetMetadataRefreshKey(surface, "tags", repository.Name, null);
        if (!IsMetadataRefreshDue(refreshKey, options.Mirror.MetadataRefreshInterval))
        {
            return null;
        }

        var refreshLock = MetadataRefreshLocks.GetOrAdd(refreshKey, static _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsMetadataRefreshDue(refreshKey, options.Mirror.MetadataRefreshInterval))
            {
                return null;
            }

            var discoveryLimit = Math.Clamp(options.Mirror.TrackedTagDiscoveryLimit, 1, 1000);
            var upstreamTags = await _mirror.TryListTagsAsync(
                surface,
                repository.Name,
                discoveryLimit,
                cancellationToken);
            LastMetadataRefresh[refreshKey] = DateTimeOffset.UtcNow;
            if (upstreamTags is null)
            {
                return null;
            }

            if (upstreamTags.CachingStrategy == MirrorCachingStrategy.ProxyOnly)
            {
                return upstreamTags.Tags;
            }

            if (upstreamTags.CachingStrategy != MirrorCachingStrategy.IndexAndCache
                || options.Mirror.NewHelmVersionsPerRefresh <= 0)
            {
                return null;
            }

            var existingTags = repository.Tags
                .Select(static tag => tag.Tag)
                .ToHashSet(StringComparer.Ordinal);
            var currentVersion = repository.Tags
                .Where(static tag => tag.Origin != PackageOrigin.Published)
                .Select(static tag => TryParseHelmVersion(tag.Tag, out var version) ? version : null)
                .OfType<NuGetVersion>()
                .MaxBy(static version => version, VersionComparer.VersionRelease);
            var parsedUpstreamVersions = new List<(string Tag, NuGetVersion Version)>();
            foreach (var tag in upstreamTags.Tags)
            {
                if (!existingTags.Contains(tag)
                    && TryParseHelmVersion(tag, out var version)
                    && version is not null)
                {
                    parsedUpstreamVersions.Add((tag, version));
                }
            }

            var newVersions = parsedUpstreamVersions
                .Where(candidate => currentVersion is null
                                    || VersionComparer.VersionRelease.Compare(candidate.Version, currentVersion) > 0)
                .OrderByDescending(candidate => candidate.Version, VersionComparer.VersionRelease)
                .Take(Math.Clamp(options.Mirror.NewHelmVersionsPerRefresh, 1, 64))
                .ToList();

            foreach (var candidate in newVersions)
            {
                var upstream = await _mirror.TryFetchManifestAsync(
                    surface,
                    repository.Name,
                    candidate.Tag,
                    cancellationToken);
                if (upstream is null)
                {
                    continue;
                }

                var cachingStrategy = upstream.CachingStrategy ?? _mirror.Strategy(surface);
                if (cachingStrategy != MirrorCachingStrategy.IndexAndCache)
                {
                    continue;
                }

                ParsedOciManifest parsed;
                try
                {
                    parsed = OciManifestParser.Parse(upstream.MediaType, upstream.Content);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Skipped invalid Helm manifest {Repository}:{Tag} discovered upstream",
                        repository.Name,
                        candidate.Tag);
                    continue;
                }

                if (parsed.ArtifactKind != OciArtifactKind.Helm)
                {
                    _logger.LogWarning(
                        "Skipped non-Helm manifest {Repository}:{Tag} discovered on a Helm surface",
                        repository.Name,
                        candidate.Tag);
                    continue;
                }

                await PersistMirroredManifestAsync(
                    surface,
                    scope,
                    repository.Name,
                    candidate.Tag,
                    upstream,
                    cachingStrategy,
                    cancellationToken);
                MarkMetadataRefreshed(surface, "manifest", repository.Name, candidate.Tag);
            }

            return null;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task PersistMirroredManifestAsync(
        SurfaceContext surface,
        OciScope scope,
        string repositoryName,
        string reference,
        OciUpstreamManifest upstream,
        MirrorCachingStrategy cachingStrategy,
        CancellationToken cancellationToken)
    {
        var options = _optionsAccessor.GetOptions(surface);
        if (!OciManifestParser.IsAllowedMediaType(upstream.MediaType, options.AllowUnknownMediaTypes))
        {
            throw new OciRegistryException("Manifest media type is not allowed.");
        }

        var parsed = OciManifestParser.Parse(upstream.MediaType, upstream.Content);
        ValidatePlatformPolicy(options, parsed);

        using var buffer = new MemoryStream(upstream.Content);
        var digest = await DigestBlobStore.ComputeSha256DigestAsync(buffer, cancellationToken);
        if (!string.Equals(digest, upstream.Digest, StringComparison.OrdinalIgnoreCase))
        {
            throw new OciRegistryException(
                $"Upstream manifest digest does not match content. Expected '{upstream.Digest}', computed '{digest}'.");
        }

        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
        await using var mutation = await OciMutationTransaction.BeginAsync(_context, scope, cancellationToken);
        buffer.Position = 0;
        if (!await store.ExistsAsync(algorithm, hex, cancellationToken))
        {
            await store.PutAsync(algorithm, hex, buffer, cancellationToken);
            _metrics.RecordBlobBytes(surface, upstream.Content.Length);
        }
        await EnsureBlobRecordAsync(scope, digest, upstream.Content.Length, cancellationToken);

        var origin = cachingStrategy == MirrorCachingStrategy.IndexAndCache
            ? PackageOrigin.Mirrored
            : PackageOrigin.Cached;
        await EnsureManifestRecordAsync(scope, digest, upstream.MediaType, parsed, upstream.Content.Length, origin, cancellationToken);
        await EnsureRepositoryAsync(scope, repositoryName, cancellationToken);
        await UpsertTagAsync(scope, repositoryName, reference, digest, origin, cancellationToken);
        if (parsed.Subject is not null
            && await ResolveManifestReferenceAsync(
                scope,
                repositoryName,
                parsed.Subject.Digest,
                cancellationToken) is not null)
        {
            await UpsertTagAsync(scope, repositoryName, digest, digest, origin, cancellationToken);
            await UpdateReferrersIndexAsync(
                surface,
                scope,
                repositoryName,
                parsed.Subject.Digest,
                new OciReferrerDescriptor(
                    parsed.MediaType,
                    digest,
                    upstream.Content.LongLength,
                    parsed.ArtifactType,
                    parsed.Annotations),
                origin,
                cancellationToken);
        }

        await mutation.CommitAsync(cancellationToken);
        _logger.LogInformation(
            "Mirrored OCI manifest {Digest} to {Repository} on feed {FeedId} segment {Segment}",
            digest,
            repositoryName,
            scope.FeedId,
            scope.OciSegment ?? "(default)");
    }

    private async Task UpdateReferrersIndexAsync(
        SurfaceContext surface,
        OciScope scope,
        string repositoryName,
        string subjectDigest,
        OciReferrerDescriptor descriptor,
        PackageOrigin origin,
        CancellationToken cancellationToken)
    {
        var lockKey = $"{scope.FeedId}\n{scope.OciSegment}\n{repositoryName}\n{subjectDigest}";
        var indexLock = ReferrerIndexLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await indexLock.WaitAsync(cancellationToken);
        try
        {
            var referrersTag = OciReferrersIndex.BuildTag(subjectDigest);
            var existing = await ReadStoredManifestAsync(
                surface,
                scope,
                repositoryName,
                referrersTag,
                cancellationToken);
            var descriptors = existing is null
                ? []
                : OciReferrersIndex.Parse(existing.Content).ToList();
            descriptors.RemoveAll(item => item.Digest.Equals(descriptor.Digest, StringComparison.OrdinalIgnoreCase));
            descriptors.Add(descriptor);

            var content = OciReferrersIndex.Serialize(descriptors);
            using var buffer = new MemoryStream(content, writable: false);
            var digest = await DigestBlobStore.ComputeSha256DigestAsync(buffer, cancellationToken);
            var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
            var store = GetDigestStore(surface);
            buffer.Position = 0;
            if (!await store.ExistsAsync(algorithm, hex, cancellationToken))
            {
                await store.PutAsync(algorithm, hex, buffer, cancellationToken);
                await EnsureBlobRecordAsync(scope, digest, content.Length, cancellationToken);
                _metrics.RecordBlobBytes(surface, content.Length);
            }

            var parsed = OciManifestParser.Parse(OciReferrersIndex.MediaType, content);
            await EnsureManifestRecordAsync(
                scope,
                digest,
                OciReferrersIndex.MediaType,
                parsed,
                content.Length,
                origin,
                cancellationToken);
            await UpsertTagAsync(scope, repositoryName, referrersTag, digest, origin, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new OciRegistryException(
                $"The existing referrers index for subject '{subjectDigest}' is invalid: {exception.Message}");
        }
        finally
        {
            indexLock.Release();
        }
    }

    private async Task<OciManifestResult?> ReadStoredManifestAsync(
        SurfaceContext surface,
        OciScope scope,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveManifestReferenceAsync(scope, repositoryName, reference, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        var manifest = await _context.OciManifests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.FeedId == scope.FeedId
                        && item.OciSegment == scope.OciSegment
                        && item.Digest == resolved.Digest,
                cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var store = GetDigestStore(surface);
        var (algorithm, hex) = DigestBlobStore.ParseDigest(resolved.Digest);
        try
        {
            await using var stream = await store.GetAsync(algorithm, hex, cancellationToken);
            using var content = new MemoryStream();
            await stream.CopyToAsync(content, cancellationToken);
            return new OciManifestResult(resolved.Digest, manifest.MediaType, content.ToArray());
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private async Task<ResolvedOciReference?> ResolveManifestReferenceAsync(
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
            var matchingTag = repository.Tags.FirstOrDefault(t =>
                string.Equals(t.ManifestDigest, reference, StringComparison.OrdinalIgnoreCase));
            return matchingTag is null
                ? null
                : new ResolvedOciReference(reference, matchingTag.Origin);
        }

        var tag = repository.Tags.FirstOrDefault(t => t.Tag == reference);
        return tag is null
            ? null
            : new ResolvedOciReference(tag.ManifestDigest, tag.Origin);
    }

    private async Task EnsureRepositoryAsync(OciScope scope, string repositoryName, CancellationToken cancellationToken)
    {
        if (await _context.OciRepositories.AnyAsync(
                r => r.FeedId == scope.FeedId
                     && r.OciSegment == scope.OciSegment
                     && r.Name == repositoryName,
                cancellationToken))
        {
            return;
        }

        try
        {
            _context.OciRepositories.Add(new OciRepository
            {
                FeedId = scope.FeedId,
                OciSegment = scope.OciSegment,
                Name = repositoryName,
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (_context.IsUniqueConstraintViolationException(ex))
        {
            // Parallel layer uploads can race on first push to a new repository.
            DetachPendingOciRepositories();
        }

        if (!await _context.OciRepositories.AnyAsync(
                r => r.FeedId == scope.FeedId
                     && r.OciSegment == scope.OciSegment
                     && r.Name == repositoryName,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"Failed to ensure OCI repository '{repositoryName}' exists.");
        }
    }

    private void DetachPendingOciRepositories()
    {
        if (_context is not DbContext dbContext)
        {
            return;
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<OciRepository>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task EnsureManifestRecordAsync(
        OciScope scope,
        string digest,
        string mediaType,
        ParsedOciManifest parsed,
        long size,
        PackageOrigin origin,
        CancellationToken cancellationToken)
    {
        var existing = await _context.OciManifests.FirstOrDefaultAsync(
            m => m.FeedId == scope.FeedId
                 && m.OciSegment == scope.OciSegment
                 && m.Digest == digest,
            cancellationToken);

        if (existing is not null)
        {
            existing.MediaType = mediaType;
            existing.PlatformOs = parsed.PlatformOs;
            existing.PlatformArch = parsed.PlatformArch;
            existing.ArtifactKind = parsed.ArtifactKind;
            existing.Origin = origin;
            existing.Size = size;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var record = new OciManifest
        {
            FeedId = scope.FeedId,
            OciSegment = scope.OciSegment,
            Digest = digest,
            MediaType = mediaType,
            PlatformOs = parsed.PlatformOs,
            PlatformArch = parsed.PlatformArch,
            ArtifactKind = parsed.ArtifactKind,
            Origin = origin,
            Size = size,
        };
        _context.OciManifests.Add(record);
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
        PackageOrigin origin,
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
                Origin = origin,
            });
        }
        else
        {
            existing.ManifestDigest = digest;
            existing.Origin = origin;
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
                 && !u.IsCancelled
                 && u.FeedId == scope.FeedId
                 && u.OciSegment == scope.OciSegment
                 && u.RepositoryName == repositoryName,
            cancellationToken);

        return upload ?? throw new OciRegistryException($"Upload '{uploadId}' was not found.", 404, "BLOB_UPLOAD_UNKNOWN");
    }

    private IDigestBlobStore GetDigestStore(SurfaceContext surface) =>
        _storageFactory.CreateDigestStore(OciSurfaceOptionsBuilder.GetStorageSubPrefix(surface.OciSegment));

    private IPathBlobStore GetPathStore(SurfaceContext surface) =>
        _storageFactory.CreatePathStore(OciSurfaceOptionsBuilder.GetStorageSubPrefix(surface.OciSegment));

    private static string BuildStagingPath(string operation) =>
        $"v2/staging/{operation}/{Guid.NewGuid():N}/data";

    private static long? TryGetStreamLength(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Length : null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static async Task<Stream> OpenPathOrEmptyAsync(
        IPathBlobStore store,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await store.GetAsync(path, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return new MemoryStream();
        }
        catch (DirectoryNotFoundException)
        {
            return new MemoryStream();
        }
    }

    private static async Task TryDeletePathAsync(IPathBlobStore store, string path)
    {
        try
        {
            await store.DeleteAsync(path, CancellationToken.None);
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static void EnsureStreamWasConsumed(OverlayReadStream stream, string operation)
    {
        if (!stream.IsComplete)
        {
            throw new OciRegistryException(
                $"The storage backend did not consume the complete {operation} stream.");
        }
    }

    private static void EnsureStreamWasConsumed(Sha256TrackingReadStream stream, string operation)
    {
        if (!stream.IsComplete)
        {
            throw new OciRegistryException(
                $"The storage backend did not consume the complete {operation} stream.");
        }
    }

    private static OciScope ToScope(SurfaceContext surface) =>
        new(surface.FeedId, surface.OciSegment);

    private static bool IsDigestReference(string reference) =>
        reference.Contains(':', StringComparison.Ordinal);

    private static bool TryParseHelmVersion(string tag, out NuGetVersion? version)
    {
        var candidate = tag;
        if (candidate.Length > 1
            && candidate[0] is 'v' or 'V'
            && char.IsAsciiDigit(candidate[1]))
        {
            candidate = candidate[1..];
        }

        return NuGetVersion.TryParse(candidate.Replace('_', '+'), out version);
    }

    private static bool IsMetadataRefreshDue(string key, TimeSpan interval) =>
        interval <= TimeSpan.Zero
        || !LastMetadataRefresh.TryGetValue(key, out var refreshedAt)
        || DateTimeOffset.UtcNow - refreshedAt >= interval;

    private static string GetMetadataRefreshKey(
        SurfaceContext surface,
        string operation,
        string repositoryName,
        string? reference) =>
        $"{surface.FeedId}\n{surface.OciSegment}\n{operation}\n{repositoryName}\n{reference}";

    private static void MarkMetadataRefreshed(
        SurfaceContext surface,
        string operation,
        string repositoryName,
        string? reference) =>
        LastMetadataRefresh[GetMetadataRefreshKey(surface, operation, repositoryName, reference)] = DateTimeOffset.UtcNow;

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

    private sealed record ResolvedOciReference(string Digest, PackageOrigin Origin);
}

public sealed class OciRegistryException : Exception
{
    public OciRegistryException(string message, int statusCode = 400, string code = "BLOB_UPLOAD_INVALID", long? rangeEnd = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        RangeEnd = rangeEnd;
    }

    public int StatusCode { get; }
    public string Code { get; }
    public long? RangeEnd { get; }
}
