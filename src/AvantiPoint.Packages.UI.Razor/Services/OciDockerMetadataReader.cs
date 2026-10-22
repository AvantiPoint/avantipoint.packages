using System.Globalization;
using System.Text.Json;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Projects OCI/Docker manifest and image-config JSON into bounded, display-safe metadata.
/// </summary>
public static class OciDockerMetadataReader
{
    public const long MaximumConfigBytes = 4 * 1024 * 1024;
    private const int MaximumCollectionItems = 100;
    private const int MaximumHistoryItems = 50;
    private const int MaximumTextLength = 2_048;
    private const string DockerConfigMediaType = "application/vnd.docker.container.image.v1+json";
    private const string OciConfigMediaType = "application/vnd.oci.image.config.v1+json";

    public static OciDockerManifestMetadata ReadManifest(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return OciDockerManifestMetadata.Empty;
            }

            var configDigest = ReadConfigDigest(root);
            var layers = ReadLayers(root);
            var platforms = ReadPlatforms(root);
            var annotations = ReadStringMap(root, "annotations");
            return new OciDockerManifestMetadata(configDigest, layers, platforms, annotations);
        }
        catch (JsonException)
        {
            return OciDockerManifestMetadata.Empty;
        }
    }

    public static OciDockerImageConfiguration ReadConfiguration(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return OciDockerImageConfiguration.Empty;
            }

            var runtime = root.TryGetProperty("config", out var config)
                          && config.ValueKind == JsonValueKind.Object
                ? config
                : default;

            return new OciDockerImageConfiguration(
                ReadTimestamp(root, "created"),
                ReadString(root, "os"),
                ReadString(root, "architecture"),
                ReadString(root, "variant"),
                ReadStringArray(runtime, "Entrypoint"),
                ReadStringArray(runtime, "Cmd"),
                ReadString(runtime, "WorkingDir"),
                ReadString(runtime, "User"),
                ReadLabels(runtime),
                ReadHistory(root));
        }
        catch (JsonException)
        {
            return OciDockerImageConfiguration.Empty;
        }
    }

    public static OciDockerImageDetails ToDetails(
        OciDockerManifestMetadata manifest,
        OciDockerImageConfiguration? configuration = null)
    {
        configuration ??= OciDockerImageConfiguration.Empty;
        return new OciDockerImageDetails(
            configuration.ImageCreatedAt,
            configuration.OperatingSystem,
            configuration.Architecture,
            configuration.Variant,
            configuration.Entrypoint,
            configuration.Command,
            configuration.WorkingDirectory,
            configuration.User,
            configuration.Labels,
            manifest.Layers,
            configuration.History,
            manifest.Platforms,
            manifest.Annotations);
    }

    private static string? ReadConfigDigest(JsonElement root)
    {
        if (!root.TryGetProperty("config", out var config)
            || config.ValueKind != JsonValueKind.Object
            || ReadString(config, "mediaType") is not { } type
            || ReadString(config, "digest") is not { } digest)
        {
            return null;
        }

        return type is DockerConfigMediaType or OciConfigMediaType
            ? digest
            : null;
    }

    private static IReadOnlyList<OciDockerLayer> ReadLayers(JsonElement root)
    {
        if (!root.TryGetProperty("layers", out var layers) || layers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<OciDockerLayer>();
        foreach (var layer in layers.EnumerateArray().Take(MaximumCollectionItems))
        {
            var digest = ReadString(layer, "digest");
            if (string.IsNullOrEmpty(digest))
            {
                continue;
            }

            result.Add(new OciDockerLayer(
                result.Count + 1,
                digest,
                ReadString(layer, "mediaType"),
                ReadInt64(layer, "size")));
        }

        return result;
    }

    private static IReadOnlyList<OciDockerPlatform> ReadPlatforms(JsonElement root)
    {
        if (!root.TryGetProperty("manifests", out var manifests)
            || manifests.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<OciDockerPlatform>();
        foreach (var descriptor in manifests.EnumerateArray().Take(MaximumCollectionItems))
        {
            var digest = ReadString(descriptor, "digest");
            if (string.IsNullOrEmpty(digest))
            {
                continue;
            }

            var platform = descriptor.TryGetProperty("platform", out var value)
                           && value.ValueKind == JsonValueKind.Object
                ? value
                : default;
            result.Add(new OciDockerPlatform(
                digest,
                ReadString(platform, "os"),
                ReadString(platform, "architecture"),
                ReadString(platform, "variant"),
                ReadInt64(descriptor, "size")));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> ReadLabels(JsonElement config)
    {
        return ReadStringMap(config, "Labels");
    }

    private static IReadOnlyDictionary<string, string> ReadStringMap(
        JsonElement parent,
        string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var values)
            || values.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var value in values.EnumerateObject()
                     .Where(value => value.Value.ValueKind == JsonValueKind.String)
                     .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                     .Take(MaximumCollectionItems))
        {
            result.TryAdd(Truncate(value.Name), Truncate(value.Value.GetString()));
        }

        return result;
    }

    private static IReadOnlyList<OciDockerHistoryEntry> ReadHistory(JsonElement root)
    {
        if (!root.TryGetProperty("history", out var history)
            || history.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        // The image configuration stores history oldest first. The UI is more useful with
        // the most recent build instruction first, and the bound prevents giant configs from
        // producing giant pages.
        return history.EnumerateArray()
            .Reverse()
            .Take(MaximumHistoryItems)
            .Select(entry => new OciDockerHistoryEntry(
                ReadTimestamp(entry, "created"),
                Truncate(ReadString(entry, "created_by")),
                Truncate(ReadString(entry, "comment")),
                ReadBoolean(entry, "empty_layer")))
            .ToList();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var values)
            || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Take(MaximumCollectionItems)
            .Select(value => Truncate(value.GetString()))
            .ToList();
    }

    private static string? ReadString(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return Truncate(value.GetString());
    }

    private static long? ReadInt64(JsonElement parent, string propertyName) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var result)
            ? result
            : null;

    private static bool ReadBoolean(JsonElement parent, string propertyName) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? ReadTimestamp(JsonElement parent, string propertyName)
    {
        var value = ReadString(parent, propertyName);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static string Truncate(string? value)
    {
        value ??= string.Empty;
        return value.Length <= MaximumTextLength ? value : value[..MaximumTextLength] + "…";
    }
}

public sealed record OciDockerManifestMetadata(
    string? ConfigDigest,
    IReadOnlyList<OciDockerLayer> Layers,
    IReadOnlyList<OciDockerPlatform> Platforms,
    IReadOnlyDictionary<string, string> Annotations)
{
    public static OciDockerManifestMetadata Empty { get; } = new(
        null,
        [],
        [],
        new Dictionary<string, string>());
}

public sealed record OciDockerImageConfiguration(
    DateTimeOffset? ImageCreatedAt,
    string? OperatingSystem,
    string? Architecture,
    string? Variant,
    IReadOnlyList<string> Entrypoint,
    IReadOnlyList<string> Command,
    string? WorkingDirectory,
    string? User,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<OciDockerHistoryEntry> History)
{
    public static OciDockerImageConfiguration Empty { get; } = new(
        null,
        null,
        null,
        null,
        [],
        [],
        null,
        null,
        new Dictionary<string, string>(),
        []);
}
