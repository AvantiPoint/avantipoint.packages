using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AvantiPoint.Packages.Host.Admin.Services.Publishers;

/// <summary>
/// Implements the client side of the OCI Distribution push protocol for one repository. The
/// client handles remote digest checks, monolithic blob uploads, manifest uploads, and Bearer
/// token challenges without buffering image layers in memory.
/// </summary>
internal sealed partial class OciRegistryPushClient
{
    private static readonly string[] AcceptedManifestMediaTypes =
    [
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.v2+json",
        "application/vnd.oci.image.index.v1+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
    ];

    private readonly HttpClient _httpClient;
    private readonly Uri _registryBaseUri;
    private readonly string _repository;
    private readonly string? _username;
    private readonly string? _secret;
    private AuthenticationHeaderValue? _bearerAuthorization;

    public OciRegistryPushClient(
        HttpClient httpClient,
        Uri registryBaseUri,
        string repository,
        string? username,
        string? secret)
    {
        _httpClient = httpClient;
        _registryBaseUri = registryBaseUri;
        _repository = repository;
        _username = username;
        _secret = secret;
    }

    public async Task<bool> ManifestMatchesAsync(
        string reference,
        string expectedDigest,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            (_, _) => Task.FromResult(CreateManifestHeadRequest(reference)),
            "pull",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        EnsureSuccess(response, "check manifest");
        return response.Headers.TryGetValues("Docker-Content-Digest", out var values)
            && values.Any(value => string.Equals(value, expectedDigest, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> BlobExistsAsync(string digest, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            (_, _) => Task.FromResult(new HttpRequestMessage(
                HttpMethod.Head,
                BuildApiUri($"blobs/{EncodePathSegment(digest)}"))),
            "pull",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        EnsureSuccess(response, "check blob");
        return true;
    }

    public async Task UploadBlobAsync(
        string digest,
        long size,
        Func<CancellationToken, Task<Stream>> openContent,
        CancellationToken cancellationToken)
    {
        using var startResponse = await SendAsync(
            (_, _) => Task.FromResult(new HttpRequestMessage(
                HttpMethod.Post,
                BuildApiUri("blobs/uploads/"))),
            "pull,push",
            cancellationToken);
        if (startResponse.StatusCode != HttpStatusCode.Accepted)
        {
            EnsureSuccess(startResponse, "start blob upload");
            throw new OciRegistryPushException(
                $"Registry returned {(int)startResponse.StatusCode} while starting a blob upload.");
        }

        var location = startResponse.Headers.Location
            ?? throw new OciRegistryPushException("Registry did not return a blob upload location.");
        var uploadUri = AppendDigest(ResolveLocation(location), digest);

        using var completeResponse = await SendAsync(
            async (_, token) =>
            {
                var content = new StreamContent(await openContent(token));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.ContentLength = size;
                return new HttpRequestMessage(HttpMethod.Put, uploadUri) { Content = content };
            },
            "pull,push",
            cancellationToken);

        if (completeResponse.StatusCode != HttpStatusCode.Created)
        {
            EnsureSuccess(completeResponse, "complete blob upload");
            throw new OciRegistryPushException(
                $"Registry returned {(int)completeResponse.StatusCode} while completing a blob upload.");
        }
    }

    public async Task PutManifestAsync(
        string reference,
        string mediaType,
        byte[] content,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            (_, _) =>
            {
                var requestContent = new ByteArrayContent(content);
                requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
                return Task.FromResult(new HttpRequestMessage(
                    HttpMethod.Put,
                    BuildApiUri($"manifests/{EncodePathSegment(reference)}"))
                {
                    Content = requestContent,
                });
            },
            "pull,push",
            cancellationToken);

        if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.Accepted))
        {
            EnsureSuccess(response, "publish manifest");
            throw new OciRegistryPushException(
                $"Registry returned {(int)response.StatusCode} while publishing a manifest.");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<AuthenticationHeaderValue?, CancellationToken, Task<HttpRequestMessage>> requestFactory,
        string scopeActions,
        CancellationToken cancellationToken)
    {
        var authorization = _bearerAuthorization ?? CreateInitialAuthorization();
        var response = await SendOnceAsync(requestFactory, authorization, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var challenge = response.Headers.WwwAuthenticate.FirstOrDefault(
            value => value.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase));
        if (challenge is null)
        {
            return response;
        }

        response.Dispose();
        _bearerAuthorization = await ExchangeBearerTokenAsync(challenge, scopeActions, cancellationToken);
        return await SendOnceAsync(requestFactory, _bearerAuthorization, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Func<AuthenticationHeaderValue?, CancellationToken, Task<HttpRequestMessage>> requestFactory,
        AuthenticationHeaderValue? authorization,
        CancellationToken cancellationToken)
    {
        using var request = await requestFactory(authorization, cancellationToken);
        if (authorization is not null && IsSameAuthority(request.RequestUri, _registryBaseUri))
        {
            request.Headers.Authorization = authorization;
        }

        return await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private async Task<AuthenticationHeaderValue> ExchangeBearerTokenAsync(
        AuthenticationHeaderValue challenge,
        string scopeActions,
        CancellationToken cancellationToken)
    {
        var parameters = ParseChallengeParameters(challenge.Parameter);
        if (!parameters.TryGetValue("realm", out var realm)
            || !Uri.TryCreate(realm, UriKind.Absolute, out var realmUri))
        {
            throw new OciRegistryPushException("Registry returned an invalid Bearer authentication challenge.");
        }

        var query = new List<string>();
        if (parameters.TryGetValue("service", out var service) && !string.IsNullOrEmpty(service))
        {
            query.Add($"service={Uri.EscapeDataString(service)}");
        }

        var scope = parameters.TryGetValue("scope", out var challengedScope)
            ? challengedScope
            : $"repository:{_repository}:{scopeActions}";
        query.Add($"scope={Uri.EscapeDataString(scope)}");

        var separator = string.IsNullOrEmpty(realmUri.Query) ? "?" : "&";
        var tokenUri = new Uri(realmUri + separator + string.Join("&", query));
        using var request = new HttpRequestMessage(HttpMethod.Get, tokenUri);
        request.Headers.Authorization = CreateTokenServiceAuthorization();

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        EnsureSuccess(response, "request registry Bearer token");

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var token = root.TryGetProperty("token", out var tokenProperty)
            ? tokenProperty.GetString()
            : root.TryGetProperty("access_token", out var accessTokenProperty)
                ? accessTokenProperty.GetString()
                : null;
        if (string.IsNullOrEmpty(token))
        {
            throw new OciRegistryPushException("Registry token service did not return a token.");
        }

        return new AuthenticationHeaderValue("Bearer", token);
    }

    private AuthenticationHeaderValue? CreateInitialAuthorization()
    {
        if (string.IsNullOrEmpty(_secret))
        {
            return null;
        }

        return string.IsNullOrEmpty(_username)
            ? new AuthenticationHeaderValue("Bearer", _secret)
            : CreateBasicAuthorization(_username, _secret);
    }

    private AuthenticationHeaderValue? CreateTokenServiceAuthorization()
    {
        if (string.IsNullOrEmpty(_secret))
        {
            return null;
        }

        return string.IsNullOrEmpty(_username)
            ? new AuthenticationHeaderValue("Bearer", _secret)
            : CreateBasicAuthorization(_username, _secret);
    }

    private static AuthenticationHeaderValue CreateBasicAuthorization(string username, string password)
    {
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return new AuthenticationHeaderValue("Basic", value);
    }

    private HttpRequestMessage CreateManifestHeadRequest(string reference)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Head,
            BuildApiUri($"manifests/{EncodePathSegment(reference)}"));
        foreach (var mediaType in AcceptedManifestMediaTypes)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        }

        return request;
    }

    private Uri BuildApiUri(string relativePath) =>
        new(_registryBaseUri, $"v2/{EncodeRepository(_repository)}/{relativePath}");

    private Uri ResolveLocation(Uri location) =>
        location.IsAbsoluteUri ? location : new Uri(_registryBaseUri, location);

    private static Uri AppendDigest(Uri location, string digest)
    {
        var builder = new UriBuilder(location);
        var query = builder.Query.TrimStart('?');
        var digestParameter = $"digest={Uri.EscapeDataString(digest)}";
        builder.Query = string.IsNullOrEmpty(query) ? digestParameter : $"{query}&{digestParameter}";
        return builder.Uri;
    }

    private static string EncodeRepository(string repository) =>
        string.Join('/', repository.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(EncodePathSegment));

    private static string EncodePathSegment(string value) => Uri.EscapeDataString(value);

    private static bool IsSameAuthority(Uri? left, Uri right) =>
        left is not null
        && left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase)
        && left.IdnHost.Equals(right.IdnHost, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

    private static IReadOnlyDictionary<string, string> ParseChallengeParameters(string? parameter)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return values;
        }

        foreach (Match match in ChallengeParameterRegex().Matches(parameter))
        {
            values[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return values;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new OciRegistryPushException(
                $"Registry returned {(int)response.StatusCode} ({response.ReasonPhrase}) while attempting to {operation}.");
        }
    }

    [GeneratedRegex("(?<name>[A-Za-z][A-Za-z0-9_-]*)=\\\"(?<value>[^\\\"]*)\\\"")]
    private static partial Regex ChallengeParameterRegex();
}

internal sealed class OciRegistryPushException(string message) : Exception(message);
