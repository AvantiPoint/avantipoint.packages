using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Registry.Oci;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

namespace AvantiPoint.Packages.Host.Admin.Services.Publishers;

/// <summary>
/// Publishes a locally hosted OCI repository tag to an external registry. Referenced manifests
/// and blobs are traversed by digest, checked remotely, and uploaded before the requested tag.
/// </summary>
public sealed class OciDownstreamPublisher(
    IContext context,
    IStorageBackendFactory storageFactory,
    ISecretProtector secretProtector,
    IHttpClientFactory httpClientFactory,
    ILogger<OciDownstreamPublisher> logger) : IDownstreamPublisher
{
    public PublishTargetProtocol Protocol => PublishTargetProtocol.Oci;

    public async Task<bool> PushAsync(
        DownstreamPublishRequest request,
        HostPublishTarget target,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var artifact = await ResolveArtifactAsync(request, cancellationToken);
            if (artifact is null)
            {
                logger.LogWarning(
                    "OCI artifact {Repository} {Tag} was not found or was ambiguous",
                    request.ArtifactName,
                    request.Version ?? "(latest)");
                return false;
            }

            var (registryBaseUri, targetRepository) = ResolveTarget(target, artifact.RepositoryName);
            var secret = secretProtector.Unprotect(target.ApiToken);
            var client = new OciRegistryPushClient(
                httpClientFactory.CreateClient(nameof(OciDownstreamPublisher)),
                registryBaseUri,
                targetRepository,
                target.Username,
                secret);

            var state = new PushState(artifact, client);
            await PushManifestAsync(
                state,
                artifact.ManifestDigest,
                artifact.Tag,
                cancellationToken);

            logger.LogInformation(
                "Published OCI repository {Repository}:{Tag} to {TargetRepository} on {Target}",
                artifact.RepositoryName,
                artifact.Tag,
                targetRepository,
                target.Name);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or TaskCanceledException
                or OciRegistryPushException
                or InvalidOperationException
                or IOException
                or UnauthorizedAccessException
                or CryptographicException
                or JsonException)
        {
            logger.LogWarning(
                exception,
                "Failed to publish OCI repository {Repository} {Tag} to {Target}",
                request.ArtifactName,
                request.Version ?? "(latest)",
                target.Name);
            return false;
        }
    }

    private async Task PushManifestAsync(
        PushState state,
        string digest,
        string reference,
        CancellationToken cancellationToken)
    {
        var publishKey = $"{reference}\n{digest}";
        if (state.PublishedManifestReferences.Contains(publishKey)
            || await state.Client.ManifestMatchesAsync(reference, digest, cancellationToken))
        {
            state.PublishedManifestReferences.Add(publishKey);
            return;
        }

        if (!state.VisitingManifests.Add(digest))
        {
            throw new InvalidOperationException($"OCI manifest graph contains a cycle at '{digest}'.");
        }

        try
        {
            var manifest = await context.OciManifests
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    m => m.FeedId == state.Artifact.FeedId
                         && m.OciSegment == state.Artifact.OciSegment
                         && m.Digest == digest,
                    cancellationToken)
                ?? throw new InvalidOperationException($"OCI manifest '{digest}' was not found locally.");

            var content = await ReadDigestAsync(state.Artifact.OciSegment, digest, cancellationToken);
            var parsed = OciManifestParser.Parse(manifest.MediaType, content);
            foreach (var referencedDigest in parsed.ReferencedDigests.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await IsManifestAsync(state.Artifact, referencedDigest, cancellationToken))
                {
                    await PushManifestAsync(
                        state,
                        referencedDigest,
                        referencedDigest,
                        cancellationToken);
                }
                else
                {
                    await PushBlobAsync(state, referencedDigest, cancellationToken);
                }
            }

            await state.Client.PutManifestAsync(reference, manifest.MediaType, content, cancellationToken);
            state.PublishedManifestReferences.Add(publishKey);
        }
        finally
        {
            state.VisitingManifests.Remove(digest);
        }
    }

    private async Task PushBlobAsync(
        PushState state,
        string digest,
        CancellationToken cancellationToken)
    {
        if (!state.PublishedBlobs.Add(digest)
            || await state.Client.BlobExistsAsync(digest, cancellationToken))
        {
            return;
        }

        var blob = await context.OciBlobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.FeedId == state.Artifact.FeedId
                     && b.OciSegment == state.Artifact.OciSegment
                     && b.Digest == digest,
                cancellationToken)
            ?? throw new InvalidOperationException($"OCI blob '{digest}' was not found locally.");

        await state.Client.UploadBlobAsync(
            digest,
            blob.Size,
            token => OpenDigestAsync(state.Artifact.OciSegment, digest, token),
            cancellationToken);
    }

    private async Task<LocalOciArtifact?> ResolveArtifactAsync(
        DownstreamPublishRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedName = request.ArtifactName.ToLowerInvariant();
        var query = context.OciRepositories
            .AsNoTracking()
            .Include(repository => repository.Tags)
            .Where(repository => repository.Name == normalizedName);

        if (request.SourceSurface is not null)
        {
            query = query.Where(repository =>
                repository.FeedId == request.SourceSurface.FeedId
                && repository.OciSegment == request.SourceSurface.OciSegment);
        }

        var repositories = await query.ToListAsync(cancellationToken);
        var candidates = new List<LocalOciArtifact>();
        foreach (var repository in repositories)
        {
            var tags = repository.Tags
                .Where(tag => tag.Origin == PackageOrigin.Published)
                .ToList();
            var selectedTag = request.Version is not null
                ? tags.FirstOrDefault(tag => tag.Tag == request.Version)
                : await SelectDefaultTagAsync(repository, tags, cancellationToken);
            if (selectedTag is not null)
            {
                candidates.Add(new LocalOciArtifact(
                    repository.FeedId,
                    repository.OciSegment,
                    repository.Name,
                    selectedTag.Tag,
                    selectedTag.ManifestDigest));
            }
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private async Task<OciTag?> SelectDefaultTagAsync(
        OciRepository repository,
        IReadOnlyList<OciTag> tags,
        CancellationToken cancellationToken)
    {
        var latest = tags.FirstOrDefault(tag => tag.Tag == "latest");
        if (latest is not null || tags.Count == 0)
        {
            return latest;
        }

        var digests = tags.Select(tag => tag.ManifestDigest).Distinct().ToList();
        var createdAtByDigest = await context.OciManifests
            .AsNoTracking()
            .Where(manifest =>
                manifest.FeedId == repository.FeedId
                && manifest.OciSegment == repository.OciSegment
                && digests.Contains(manifest.Digest))
            .ToDictionaryAsync(manifest => manifest.Digest, manifest => manifest.CreatedAt, cancellationToken);

        return tags
            .OrderByDescending(tag => createdAtByDigest.GetValueOrDefault(tag.ManifestDigest))
            .ThenByDescending(tag => tag.Tag, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private Task<bool> IsManifestAsync(
        LocalOciArtifact artifact,
        string digest,
        CancellationToken cancellationToken) =>
        context.OciManifests.AsNoTracking().AnyAsync(
            manifest => manifest.FeedId == artifact.FeedId
                        && manifest.OciSegment == artifact.OciSegment
                        && manifest.Digest == digest,
            cancellationToken);

    private async Task<byte[]> ReadDigestAsync(
        string? ociSegment,
        string digest,
        CancellationToken cancellationToken)
    {
        await using var stream = await OpenDigestAsync(ociSegment, digest, cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private Task<Stream> OpenDigestAsync(
        string? ociSegment,
        string digest,
        CancellationToken cancellationToken)
    {
        var store = storageFactory.CreateDigestStore(OciSurfaceOptionsBuilder.GetStorageSubPrefix(ociSegment));
        var (algorithm, hex) = DigestBlobStore.ParseDigest(digest);
        return store.GetAsync(algorithm, hex, cancellationToken);
    }

    private static (Uri RegistryBaseUri, string Repository) ResolveTarget(
        HostPublishTarget target,
        string sourceRepository)
    {
        if (!Uri.TryCreate(target.PublishEndpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"OCI publish target '{target.Name}' has an invalid endpoint.");
        }

        if (!string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new InvalidOperationException("OCI publish endpoints cannot contain a query or fragment.");
        }

        var repositoryPrefix = Uri.UnescapeDataString(endpoint.AbsolutePath).Trim('/');
        if (repositoryPrefix.Equals("v2", StringComparison.OrdinalIgnoreCase)
            || repositoryPrefix.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("OCI publish endpoints must not include the /v2 API path.");
        }

        var repository = string.IsNullOrEmpty(repositoryPrefix)
            ? sourceRepository
            : $"{repositoryPrefix}/{sourceRepository}";
        var registryBaseUri = new Uri(endpoint.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/");
        return (registryBaseUri, repository.ToLowerInvariant());
    }

    private sealed record LocalOciArtifact(
        string FeedId,
        string? OciSegment,
        string RepositoryName,
        string Tag,
        string ManifestDigest);

    private sealed class PushState(LocalOciArtifact artifact, OciRegistryPushClient client)
    {
        public LocalOciArtifact Artifact { get; } = artifact;

        public OciRegistryPushClient Client { get; } = client;

        public HashSet<string> VisitingManifests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> PublishedManifestReferences { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> PublishedBlobs { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
