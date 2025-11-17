using System.Collections.Generic;
using System.Text.Json;

namespace OpenFeed.Services;

public sealed class OpenApiDocument
{
    public required string Title { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyList<OpenApiPath> Paths { get; init; }

    public static OpenApiDocument Parse(JsonElement root)
    {
        var info = root.GetProperty("info");
        var title = info.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "API" : "API";
        var version = info.TryGetProperty("version", out var vEl) ? vEl.GetString() ?? "v1" : "v1";
        var paths = new List<OpenApiPath>();
        if (root.TryGetProperty("paths", out var pathsEl))
        {
            foreach (var p in pathsEl.EnumerateObject())
            {
                var operations = new List<OpenApiOperation>();
                foreach (var op in p.Value.EnumerateObject())
                {
                    var verb = op.Name.ToUpperInvariant();
                    var tags = new List<string>();
                    if (op.Value.TryGetProperty("tags", out var tagsEl))
                    {
                        foreach (var tag in tagsEl.EnumerateArray())
                            tags.Add(tag.GetString() ?? string.Empty);
                    }
                    var summary = op.Value.TryGetProperty("summary", out var sEl) ? sEl.GetString() : null;
                    operations.Add(new OpenApiOperation
                    {
                        Verb = verb,
                        Summary = summary,
                        Tags = tags
                    });
                }
                paths.Add(new OpenApiPath
                {
                    Route = p.Name,
                    Operations = operations
                });
            }
        }
        return new OpenApiDocument
        {
            Title = title,
            Version = version,
            Paths = paths
        };
    }
}
