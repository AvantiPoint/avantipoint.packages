using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace OpenFeed.Services;

public class OpenApiSpecProvider(HttpClient httpClient,
    IMemoryCache cache,
    ILogger<OpenApiSpecProvider> logger,
    IHttpContextAccessor httpContextAccessor) : IOpenApiSpecProvider
{
    private static readonly string CacheKey = "OpenApiDocument";
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<OpenApiDocument?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out OpenApiDocument? cached) && cached is not null) return cached;

        try
        {
            EnsureBaseAddress(httpClient);

            using var response = await httpClient.GetAsync("openapi/v1.json", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            var document = SimpleOpenApiReader.TryRead(json, logger);
            if (document is null)
            {
                logger.LogWarning("Lightweight OpenAPI reader failed; returning null document.");
                return null;
            }

            cache.Set(CacheKey, document, TimeSpan.FromMinutes(5));
            return document;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch OpenAPI document.");
            return null;
        }
    }

    private void EnsureBaseAddress(HttpClient client)
    {
        if (client.BaseAddress != null) return;

        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return;

        var scheme = ctx.Request.Scheme;
        var host = ctx.Request.Host.Value;
        var pathBase = ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.Value.TrimEnd('/') + "/" : string.Empty;
        try
        {
            client.BaseAddress = new Uri($"{scheme}://{host}/{pathBase}");
        }
        catch
        {
            // swallow - request will fail and be logged by caller
        }
    }
}