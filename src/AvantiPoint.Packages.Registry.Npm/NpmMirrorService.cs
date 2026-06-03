using System.Net.Http.Json;
using System.Text.Json;
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
    private readonly NpmFeedOptions _options;
    private readonly IMirrorPolicyService _policy;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NpmMirrorService> _logger;

    public NpmMirrorService(
        IOptions<NpmFeedOptions> options,
        IMirrorPolicyService policy,
        IHttpClientFactory httpClientFactory,
        ILogger<NpmMirrorService> logger)
    {
        _options = options.Value;
        _policy = policy;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        Strategy = _policy.GetStrategy(FeedProtocol.Npm);
        MirrorOrigin = Strategy == MirrorCachingStrategy.IndexAndCache
            ? PackageOrigin.Mirrored
            : PackageOrigin.Cached;
    }

    public MirrorCachingStrategy Strategy { get; }

    public PackageOrigin MirrorOrigin { get; }

    public async Task<JsonObject?> FetchPackumentAsync(string packageName, CancellationToken cancellationToken = default)
    {
        var upstreamUrl = BuildUpstreamPackumentUrl(packageName);
        if (upstreamUrl is null)
        {
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(NpmMirrorService));
            var packument = await client.GetFromJsonAsync<JsonObject>(upstreamUrl, cancellationToken);
            return packument;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch npm packument {Package} from upstream", packageName);
            return null;
        }
    }

    public async Task<Stream?> FetchTarballAsync(string tarballUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(NpmMirrorService));
            var response = await client.GetAsync(tarballUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return new MemoryStream(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch npm tarball from {Url}", tarballUrl);
            return null;
        }
    }

    private string? BuildUpstreamPackumentUrl(string normalizedName)
    {
        var baseUrl = _options.Mirror?.RegistryUrl?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            return null;
        }

        return $"{baseUrl}/{NpmPackageService.EncodePackagePath(normalizedName)}";
    }
}
