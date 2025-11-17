using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace OpenFeed.Services;

public class OpenApiSpecProvider(HttpClient httpClient, IMemoryCache cache, ILogger<OpenApiSpecProvider> logger) : IOpenApiSpecProvider
{
    private static readonly string CacheKey = "OpenApiDocument";

    public async Task<OpenApiDocument?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out OpenApiDocument doc)) return doc;

        try
        {
            using var response = await httpClient.GetAsync("/openapi/v1.json", cancellationToken);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            doc = OpenApiDocument.Parse(json.RootElement);
            cache.Set(CacheKey, doc, TimeSpan.FromMinutes(5));
            return doc;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch OpenAPI document.");
            return null;
        }
    }
}