using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Npm;

public interface INpmMirrorService
{
    Task<JsonObject?> FetchPackumentAsync(string packageName, CancellationToken cancellationToken = default);

    Task<Stream?> FetchTarballAsync(string tarballUrl, CancellationToken cancellationToken = default);

    MirrorCachingStrategy Strategy { get; }

    PackageOrigin MirrorOrigin { get; }
}

public sealed class NpmMirrorService : INpmMirrorService
{
    private readonly IReadOnlyList<NpmUpstreamRegistryOptions> _registries;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NpmMirrorService> _logger;

    public NpmMirrorService(
        IOptions<NpmFeedOptions> options,
        IMirrorPolicyService policy,
        IHttpClientFactory httpClientFactory,
        ILogger<NpmMirrorService> logger)
    {
        _registries = options.Value.Mirror?.GetUpstreamRegistries() ?? [];
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        Strategy = policy.GetStrategy(FeedProtocol.Npm);
        MirrorOrigin = Strategy == MirrorCachingStrategy.IndexAndCache
            ? PackageOrigin.Mirrored
            : PackageOrigin.Cached;
    }

    public MirrorCachingStrategy Strategy { get; }

    public PackageOrigin MirrorOrigin { get; }

    public async Task<JsonObject?> FetchPackumentAsync(string packageName, CancellationToken cancellationToken = default)
    {
        foreach (var registry in _registries)
        {
            var upstreamUrl = BuildUpstreamPackumentUrl(registry, packageName);
            if (upstreamUrl is null)
            {
                continue;
            }

            try
            {
                using var client = _httpClientFactory.CreateClient(nameof(NpmMirrorService));
                using var request = new HttpRequestMessage(HttpMethod.Get, upstreamUrl);
                ApplyAuthentication(request, registry);

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug(
                        "Upstream npm registry {Registry} returned {StatusCode} for {Package}",
                        registry.Url,
                        (int)response.StatusCode,
                        packageName);
                    continue;
                }

                await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
                if (JsonNode.Parse(content) is JsonObject packument)
                {
                    return packument;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Includes HttpClient timeouts (surfaced as OperationCanceledException even when
                // the caller's token is not canceled) — treat those as an upstream failure and
                // fall through to the next registry.
                _logger.LogWarning(
                    ex,
                    "Failed to fetch npm packument {Package} from upstream {Registry}",
                    packageName,
                    registry.Url);
            }
        }

        return null;
    }

    public async Task<Stream?> FetchTarballAsync(string tarballUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(NpmMirrorService));
            using var request = new HttpRequestMessage(HttpMethod.Get, tarballUrl);

            // Tarball URLs come from the upstream packument and are absolute; attach the
            // credentials of the registry that serves this host (if one is configured).
            var registry = FindRegistryForUrl(tarballUrl);
            if (registry is not null)
            {
                ApplyAuthentication(request, registry);
            }

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            response.Dispose();
            return new MemoryStream(bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch npm tarball from {Url}", tarballUrl);
            return null;
        }
    }

    private NpmUpstreamRegistryOptions? FindRegistryForUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // Prefer the registry whose full URL (origin + path) is the longest prefix of the
        // target URL — registries can share a host with different paths and credentials
        // (for example multiple Azure Artifacts feeds under pkgs.dev.azure.com).
        NpmUpstreamRegistryOptions? bestPrefixMatch = null;
        var bestPrefixLength = -1;
        NpmUpstreamRegistryOptions? hostMatch = null;

        foreach (var registry in _registries)
        {
            if (!Uri.TryCreate(registry.Url, UriKind.Absolute, out var registryUri)
                || !string.Equals(registryUri.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hostMatch ??= registry;

            var prefix = registryUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/";
            if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && prefix.Length > bestPrefixLength)
            {
                bestPrefixMatch = registry;
                bestPrefixLength = prefix.Length;
            }
        }

        // Fall back to a host-only match: tarball URLs are not always under the registry
        // path (some registries serve tarballs from a different path on the same host).
        return bestPrefixMatch ?? hostMatch;
    }

    private static void ApplyAuthentication(HttpRequestMessage request, NpmUpstreamRegistryOptions registry)
    {
        if (!string.IsNullOrWhiteSpace(registry.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", registry.Token);
            return;
        }

        if (!string.IsNullOrWhiteSpace(registry.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{registry.Username}:{registry.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    private static string? BuildUpstreamPackumentUrl(NpmUpstreamRegistryOptions registry, string normalizedName)
    {
        var baseUrl = registry.Url?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            return null;
        }

        return $"{baseUrl}/{NpmPackageService.EncodePackagePath(normalizedName)}";
    }
}
