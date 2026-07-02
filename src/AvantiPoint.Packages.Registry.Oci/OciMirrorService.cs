using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed class OciMirrorService : IOciMirrorService
{
    private readonly IOciUpstreamRegistryProvider _registryProvider;
    private readonly IMirrorPolicyService _policy;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OciMirrorService> _logger;

    public OciMirrorService(
        IOciUpstreamRegistryProvider registryProvider,
        IMirrorPolicyService policy,
        IHttpClientFactory httpClientFactory,
        ILogger<OciMirrorService> logger)
    {
        _registryProvider = registryProvider;
        _policy = policy;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public PackageOrigin MirrorOrigin(SurfaceContext surface) =>
        Strategy(surface) == MirrorCachingStrategy.IndexAndCache
            ? PackageOrigin.Mirrored
            : PackageOrigin.Cached;

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
        foreach (var registry in await GetUpstreamRegistriesAsync(surface, cancellationToken))
        {
            var url = $"{registry.Url}/v2/{repositoryName}/manifests/{reference}";
            var client = CreateClient(registry);
            using var request = CreateManifestRequest(url);

            using var response = await SendWithBearerChallengeAsync(
                client,
                request,
                () => CreateManifestRequest(url),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Upstream OCI manifest {RepositoryName}:{Reference} not found at {Url}", repositoryName, reference, url);
                continue;
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

        return null;
    }

    public async Task<Stream?> TryFetchBlobAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default)
    {
        foreach (var registry in await GetUpstreamRegistriesAsync(surface, cancellationToken))
        {
            var url = $"{registry.Url}/v2/{repositoryName}/blobs/{digest}";

            var client = CreateClient(registry);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await SendWithBearerChallengeAsync(
                client,
                request,
                () => new HttpRequestMessage(HttpMethod.Get, url),
                cancellationToken,
                HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Upstream OCI blob {Digest} not found at {Url}", digest, url);
                continue;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return new MemoryStream(bytes);
        }

        return null;
    }

    public async Task<OciBlobExistsResult?> TryCheckBlobExistsAsync(
        SurfaceContext surface,
        string repositoryName,
        string digest,
        CancellationToken cancellationToken = default)
    {
        foreach (var registry in await GetUpstreamRegistriesAsync(surface, cancellationToken))
        {
            var url = $"{registry.Url}/v2/{repositoryName}/blobs/{digest}";

            var client = CreateClient(registry);
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await SendWithBearerChallengeAsync(
                client,
                request,
                () => new HttpRequestMessage(HttpMethod.Head, url),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Upstream OCI blob {Digest} not found at {Url}", digest, url);
                continue;
            }

            var size = response.Content.Headers.ContentLength ?? 0;
            return new OciBlobExistsResult(true, size);
        }

        return new OciBlobExistsResult(false, 0);
    }

    private async Task<IReadOnlyList<OciUpstreamRegistry>> GetUpstreamRegistriesAsync(
        SurfaceContext surface,
        CancellationToken cancellationToken)
    {
        var registries = await _registryProvider.GetRegistriesAsync(surface, cancellationToken);
        return registries
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .OrderBy(r => r.Priority)
            .Select(r => new OciUpstreamRegistry(r.Url.TrimEnd('/'), r.Username, r.Password))
            .ToArray();
    }

    private HttpClient CreateClient(OciUpstreamRegistry registry)
    {
        var client = _httpClientFactory.CreateClient(nameof(OciMirrorService));
        client.DefaultRequestHeaders.Authorization = null;
        if (registry.Username is not null && registry.Password is not null)
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{registry.Username}:{registry.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        return client;
    }

    private static HttpRequestMessage CreateManifestRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.list.v2+json"));
        return request;
    }

    private async Task<HttpResponseMessage> SendWithBearerChallengeAsync(
        HttpClient client,
        HttpRequestMessage request,
        Func<HttpRequestMessage> retryRequestFactory,
        CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        var response = await client.SendAsync(request, completionOption, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !TryGetBearerChallenge(response, out var challenge))
        {
            return response;
        }

        var token = await TryFetchBearerTokenAsync(client, challenge, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return response;
        }

        response.Dispose();
        using var retryRequest = retryRequestFactory();
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(retryRequest, completionOption, cancellationToken);
    }

    private static bool TryGetBearerChallenge(HttpResponseMessage response, out OciBearerChallenge challenge)
    {
        foreach (var header in response.Headers.WwwAuthenticate)
        {
            if (!string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parameters = ParseChallengeParameters(header.Parameter);
            if (parameters.TryGetValue("realm", out var realm) && !string.IsNullOrWhiteSpace(realm))
            {
                parameters.TryGetValue("service", out var service);
                parameters.TryGetValue("scope", out var scope);
                challenge = new OciBearerChallenge(realm, service, scope);
                return true;
            }
        }

        challenge = default;
        return false;
    }

    private async Task<string?> TryFetchBearerTokenAsync(
        HttpClient client,
        OciBearerChallenge challenge,
        CancellationToken cancellationToken)
    {
        var tokenUri = BuildTokenUri(challenge);
        if (tokenUri is null || !CanRequestToken(client, tokenUri))
        {
            return null;
        }

        using var response = await client.GetAsync(tokenUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Upstream OCI token service returned {StatusCode} for {Realm}", response.StatusCode, challenge.Realm);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return TryGetStringProperty(document.RootElement, "token")
            ?? TryGetStringProperty(document.RootElement, "access_token");
    }

    private static bool CanRequestToken(HttpClient client, Uri tokenUri)
    {
        return string.Equals(tokenUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || client.DefaultRequestHeaders.Authorization is null;
    }

    private static Uri? BuildTokenUri(OciBearerChallenge challenge)
    {
        if (!Uri.TryCreate(challenge.Realm, UriKind.Absolute, out var realm))
        {
            return null;
        }

        var builder = new UriBuilder(realm);
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            query.Add(builder.Query.TrimStart('?'));
        }

        if (!string.IsNullOrWhiteSpace(challenge.Service))
        {
            query.Add($"service={Uri.EscapeDataString(challenge.Service)}");
        }

        if (!string.IsNullOrWhiteSpace(challenge.Scope))
        {
            query.Add($"scope={Uri.EscapeDataString(challenge.Scope)}");
        }

        builder.Query = string.Join("&", query);
        return builder.Uri;
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static Dictionary<string, string> ParseChallengeParameters(string? value)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return parameters;
        }

        var index = 0;
        while (index < value.Length)
        {
            while (index < value.Length && (value[index] == ',' || char.IsWhiteSpace(value[index])))
            {
                index++;
            }

            var keyStart = index;
            while (index < value.Length && value[index] != '=' && value[index] != ',')
            {
                index++;
            }

            if (index >= value.Length || value[index] != '=')
            {
                break;
            }

            var key = value[keyStart..index].Trim();
            index++;

            var parameterValue = ReadChallengeValue(value, ref index);
            if (!string.IsNullOrWhiteSpace(key))
            {
                parameters[key] = parameterValue;
            }
        }

        return parameters;
    }

    private static string ReadChallengeValue(string value, ref int index)
    {
        if (index >= value.Length || value[index] != '"')
        {
            var start = index;
            while (index < value.Length && value[index] != ',')
            {
                index++;
            }

            return value[start..index].Trim();
        }

        index++;
        var result = new StringBuilder();
        while (index < value.Length)
        {
            var current = value[index++];
            if (current == '"')
            {
                break;
            }

            if (current == '\\' && index < value.Length)
            {
                current = value[index++];
            }

            result.Append(current);
        }

        return result.ToString();
    }

    private readonly record struct OciBearerChallenge(string Realm, string? Service, string? Scope);

    private readonly record struct OciUpstreamRegistry(string Url, string? Username, string? Password);
}
