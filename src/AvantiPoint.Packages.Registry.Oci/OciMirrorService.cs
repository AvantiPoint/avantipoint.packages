using System.Net.Http.Headers;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci;

public interface IOciMirrorService
{
    Task<OciUpstreamManifest?> TryFetchManifestAsync(
        SurfaceContext surface,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken = default);

    Task<Stream?> TryFetchBlobAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default);

    Task<OciBlobExistsResult?> TryCheckBlobExistsAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default);

    PackageOrigin MirrorOrigin(SurfaceContext surface);

    MirrorCachingStrategy Strategy(SurfaceContext surface);

    bool ShouldPersist(SurfaceContext surface);
}

public sealed record OciUpstreamManifest(string Digest, string MediaType, byte[] Content);

public sealed class OciMirrorService : IOciMirrorService
{
    private readonly OciFeedOptionsAccessor _optionsAccessor;
    private readonly IMirrorPolicyService _policy;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OciMirrorService> _logger;

    public OciMirrorService(
        OciFeedOptionsAccessor optionsAccessor,
        IMirrorPolicyService policy,
        IHttpClientFactory httpClientFactory,
        ILogger<OciMirrorService> logger)
    {
        _optionsAccessor = optionsAccessor;
        _policy = policy;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public PackageOrigin MirrorOrigin(SurfaceContext surface) =>
        Strategy(surface) == MirrorCachingStrategy.ProxyOnly
            ? PackageOrigin.Cached
            : PackageOrigin.Mirrored;

    public MirrorCachingStrategy Strategy(SurfaceContext surface) =>
        _policy.GetStrategy(FeedProtocol.Oci, surface.OciSegment);

    public bool ShouldPersist(SurfaceContext surface) =>
        Strategy(surface) != MirrorCachingStrategy.ProxyOnly;

    public async Task<OciUpstreamManifest?> TryFetchManifestAsync(
        SurfaceContext surface,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken = default)
    {
        var upstream = GetUpstreamBaseUrl(surface);
        if (upstream is null)
        {
            return null;
        }

        var url = $"{upstream}/v2/{repositoryName}/manifests/{reference}";
        using var client = CreateClient(surface);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/vnd.oci.image.manifest.v1+json";
        var digest = response.Headers.TryGetValues("Docker-Content-Digest", out var values)
            ? values.First()
            : null;
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (string.IsNullOrEmpty(digest))
        {
            using var buffer = new MemoryStream(bytes);
            digest = await DigestBlobStore.ComputeSha256DigestAsync(buffer, cancellationToken);
        }

        return new OciUpstreamManifest(digest, mediaType, bytes);
    }

    public async Task<Stream?> TryFetchBlobAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default)
    {
        var upstream = GetUpstreamBaseUrl(surface);
        if (upstream is null)
        {
            return null;
        }

        var url = $"{upstream}/v2/{repositoryName}/blobs/{digest}";

        using var client = CreateClient(surface);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Upstream OCI blob {Digest} not found at {Url}", digest, url);
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new MemoryStream(bytes);
    }

    public async Task<OciBlobExistsResult?> TryCheckBlobExistsAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default)
    {
        var upstream = GetUpstreamBaseUrl(surface);
        if (upstream is null)
        {
            return null;
        }

        var url = $"{upstream}/v2/{repositoryName}/blobs/{digest}";

        using var client = CreateClient(surface);
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Upstream OCI blob {Digest} not found at {Url}", digest, url);
            return new OciBlobExistsResult(false, 0);
        }

        var size = response.Content.Headers.ContentLength ?? 0;
        return new OciBlobExistsResult(true, size);
    }

    private string? GetUpstreamBaseUrl(SurfaceContext surface)
    {
        var options = _optionsAccessor.GetOptions(surface);
        var registry = options.Mirror?.Registries?
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .OrderBy(r => r.Priority)
            .FirstOrDefault();

        return registry?.Url?.TrimEnd('/');
    }

    private HttpClient CreateClient(SurfaceContext surface)
    {
        var client = _httpClientFactory.CreateClient(nameof(OciMirrorService));
        var options = _optionsAccessor.GetOptions(surface);
        var registry = options.Mirror?.Registries?
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .OrderBy(r => r.Priority)
            .FirstOrDefault();

        if (registry?.Username is not null && registry.Password is not null)
        {
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{registry.Username}:{registry.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        return client;
    }
}
