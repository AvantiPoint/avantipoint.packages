using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace OpenFeed.Services;

internal static class SimpleOpenApiReader
{
    public static OpenApiDocument? TryRead(string json, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            logger.LogWarning("Received empty OpenAPI payload.");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ParseDocument(document.RootElement);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse OpenAPI payload using lightweight reader.");
            return null;
        }
    }

    private static OpenApiDocument ParseDocument(JsonElement root)
    {
        var openApi = new OpenApiDocument
        {
            Info = ParseInfo(root),
            Paths = ParsePaths(root)
        };

        return openApi;
    }

    private static OpenApiInfo ParseInfo(JsonElement root)
    {
        if (!root.TryGetProperty("info", out var infoElement) || infoElement.ValueKind != JsonValueKind.Object)
        {
            return new OpenApiInfo();
        }

        return new OpenApiInfo
        {
            Title = infoElement.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null,
            Version = infoElement.TryGetProperty("version", out var versionEl) ? versionEl.GetString() : null,
            Description = infoElement.TryGetProperty("description", out var descriptionEl) ? descriptionEl.GetString() : null
        };
    }

    private static OpenApiPaths ParsePaths(JsonElement root)
    {
        var paths = new OpenApiPaths();
        if (!root.TryGetProperty("paths", out var pathsElement) || pathsElement.ValueKind != JsonValueKind.Object)
        {
            return paths;
        }

        foreach (var pathProperty in pathsElement.EnumerateObject())
        {
            var pathItem = new OpenApiPathItem();
            if (pathProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var operationProperty in pathProperty.Value.EnumerateObject())
            {
                // We only use path + HTTP verb + summary + tags in the UI, so a
                // lightweight projection is sufficient; keep the OpenApiDocument
                // instance minimal and let the UI work directly from JSON if
                // richer data is needed in the future.
            }

            paths[pathProperty.Name] = pathItem;
        }

        return paths;
    }

    // NOTE: for now we omit operations-level details because the UI builds its own
    // minimal model from the official OpenAPI JSON. This reader exists only as a
    // fallback and to ensure we can surface top-level metadata without depending
    // on deprecated Microsoft.OpenApi.Readers APIs.
}
