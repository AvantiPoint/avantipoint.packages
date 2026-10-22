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

    async Task<NpmMirrorPackument?> FetchPackumentWithPolicyAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        var packument = await FetchPackumentAsync(packageName, cancellationToken);
        return packument is null ? null : new NpmMirrorPackument(packument, Strategy);
    }

    Task<Stream?> FetchTarballAsync(string tarballUrl, CancellationToken cancellationToken = default);

    ValueTask<MirrorCachingStrategy> GetTarballStrategyAsync(
        string tarballUrl,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Strategy);

    MirrorCachingStrategy Strategy { get; }

    PackageOrigin MirrorOrigin { get; }
}

public sealed record NpmMirrorPackument(JsonObject Packument, MirrorCachingStrategy Strategy)
{
    public PackageOrigin Origin => Strategy == MirrorCachingStrategy.IndexAndCache
        ? PackageOrigin.Mirrored
        : PackageOrigin.Cached;
}

public sealed class NpmMirrorService : INpmMirrorService
{
    private readonly INpmUpstreamRegistryProvider _registryProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NpmMirrorService> _logger;
    private readonly long _maxTarballBytes;

    public NpmMirrorService(
        INpmUpstreamRegistryProvider registryProvider,
        IMirrorPolicyService policy,
        IHttpClientFactory httpClientFactory,
        IOptions<NpmFeedOptions> npmOptions,
        ILogger<NpmMirrorService> logger)
    {
        _registryProvider = registryProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _maxTarballBytes = npmOptions.Value.MaxTarballBytes;
        Strategy = policy.GetStrategy(FeedProtocol.Npm);
        MirrorOrigin = Strategy == MirrorCachingStrategy.IndexAndCache
            ? PackageOrigin.Mirrored
            : PackageOrigin.Cached;
    }

    public MirrorCachingStrategy Strategy { get; }

    public PackageOrigin MirrorOrigin { get; }

    public async Task<JsonObject?> FetchPackumentAsync(
        string packageName,
        CancellationToken cancellationToken = default) =>
        (await FetchPackumentWithPolicyAsync(packageName, cancellationToken))?.Packument;

    public async Task<NpmMirrorPackument?> FetchPackumentWithPolicyAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        foreach (var registry in await _registryProvider.GetRegistriesAsync(cancellationToken))
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
                    return new NpmMirrorPackument(
                        packument,
                        registry.CachingStrategy ?? Strategy);
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

    public async ValueTask<MirrorCachingStrategy> GetTarballStrategyAsync(
        string tarballUrl,
        CancellationToken cancellationToken = default)
    {
        var registries = await _registryProvider.GetRegistriesAsync(cancellationToken);
        return FindRegistryForUrl(registries, tarballUrl)?.CachingStrategy ?? Strategy;
    }

    public async Task<Stream?> FetchTarballAsync(string tarballUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(NpmMirrorService));
            using var request = new HttpRequestMessage(HttpMethod.Get, tarballUrl);

            // Tarball URLs come from the upstream packument and are absolute; attach the
            // credentials of the registry that serves this host (if one is configured).
            var registries = await _registryProvider.GetRegistriesAsync(cancellationToken);
            var registry = FindRegistryForUrl(registries, tarballUrl);
            if (registry is not null)
            {
                ApplyAuthentication(request, registry);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            if (response.Content.Headers.ContentLength is { } contentLength
                && contentLength > _maxTarballBytes)
            {
                _logger.LogWarning(
                    "Rejected npm tarball from {Url} because its declared size of {Size} bytes exceeds the configured limit of {Limit} bytes",
                    tarballUrl,
                    contentLength,
                    _maxTarballBytes);
                throw new NpmTarballSizeLimitExceededException(_maxTarballBytes);
            }

            var temporaryTarballPath = Path.GetTempFileName();
            var bufferedTarball = new FileStream(
                temporaryTarballPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);

            try
            {
                await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[81920];
                long totalBytes = 0;
                while (true)
                {
                    var bytesRead = await content.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytes += bytesRead;
                    if (totalBytes > _maxTarballBytes)
                    {
                        _logger.LogWarning(
                            "Rejected npm tarball from {Url} because it exceeds the configured limit of {Limit} bytes",
                            tarballUrl,
                            _maxTarballBytes);
                        throw new NpmTarballSizeLimitExceededException(_maxTarballBytes);
                    }

                    await bufferedTarball.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                bufferedTarball.Position = 0;
                return bufferedTarball;
            }
            catch
            {
                await bufferedTarball.DisposeAsync();
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex is NpmTarballSizeLimitExceededException) throw;
            _logger.LogWarning(ex, "Failed to fetch npm tarball from {Url}", tarballUrl);
            return null;
        }
    }

    private static NpmUpstreamRegistryOptions? FindRegistryForUrl(
        IReadOnlyList<NpmUpstreamRegistryOptions> registries,
        string url)
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

        foreach (var registry in registries)
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
