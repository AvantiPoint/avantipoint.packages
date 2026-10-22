using System.Text.Json;

namespace AvantiPoint.Packages.Registry.Oci;

/// <summary>
/// Reads and writes the OCI 1.1 referrers image-index representation. The same bytes can
/// back the native referrers endpoint and the compatibility referrers-tag schema.
/// </summary>
public static class OciReferrersIndex
{
    public const string MediaType = "application/vnd.oci.image.index.v1+json";

    public static string BuildTag(string subjectDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectDigest);
        var separator = subjectDigest.IndexOf(':');
        if (separator <= 0 || separator == subjectDigest.Length - 1)
        {
            throw new ArgumentException("The subject digest must contain an algorithm and encoded value.", nameof(subjectDigest));
        }

        var algorithm = SanitizeTagPart(subjectDigest.AsSpan(0, Math.Min(separator, 32)));
        var encodedLength = Math.Min(subjectDigest.Length - separator - 1, 64);
        var encoded = SanitizeTagPart(subjectDigest.AsSpan(separator + 1, encodedLength));
        return $"{algorithm}-{encoded}";
    }

    public static bool IsInternalTag(string tag)
    {
        if (!(tag.StartsWith("sha256-", StringComparison.OrdinalIgnoreCase)
              || tag.StartsWith("sha512-", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var separator = tag.IndexOf('-');
        var encoded = tag.AsSpan(separator + 1);
        return encoded.Length == 64 && ContainsOnlyHexDigits(encoded);
    }

    public static byte[] Serialize(IEnumerable<OciReferrerDescriptor> descriptors)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 2);
            writer.WriteString("mediaType", MediaType);
            writer.WriteStartArray("manifests");
            foreach (var descriptor in descriptors)
            {
                writer.WriteStartObject();
                writer.WriteString("mediaType", descriptor.MediaType);
                writer.WriteString("digest", descriptor.Digest);
                writer.WriteNumber("size", descriptor.Size);
                if (!string.IsNullOrWhiteSpace(descriptor.ArtifactType))
                {
                    writer.WriteString("artifactType", descriptor.ArtifactType);
                }

                writer.WriteStartObject("annotations");
                foreach (var annotation in descriptor.Annotations.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(annotation.Key, annotation.Value);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    public static IReadOnlyList<OciReferrerDescriptor> Parse(ReadOnlyMemory<byte> content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (!root.TryGetProperty("manifests", out var manifests)
            || manifests.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<OciReferrerDescriptor>();
        foreach (var descriptor in manifests.EnumerateArray())
        {
            var mediaType = ReadString(descriptor, "mediaType");
            var digest = ReadString(descriptor, "digest");
            if (string.IsNullOrWhiteSpace(mediaType)
                || string.IsNullOrWhiteSpace(digest)
                || !descriptor.TryGetProperty("size", out var sizeElement)
                || !sizeElement.TryGetInt64(out var size)
                || size < 0)
            {
                continue;
            }

            result.Add(new OciReferrerDescriptor(
                mediaType,
                digest,
                size,
                ReadString(descriptor, "artifactType"),
                ReadStringMap(descriptor, "annotations")));
        }

        return result;
    }

    private static string SanitizeTagPart(ReadOnlySpan<char> value)
    {
        var characters = value.ToArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (!(char.IsAsciiLetterOrDigit(characters[index]) || characters[index] is '.' or '_' or '-'))
            {
                characters[index] = '-';
            }
        }

        return new string(characters);
    }

    private static bool ContainsOnlyHexDigits(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string? ReadString(JsonElement parent, string propertyName) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var map) || map.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in map.EnumerateObject()
                     .Where(static property => property.Value.ValueKind == JsonValueKind.String))
        {
            result[property.Name] = property.Value.GetString()!;
        }

        return result;
    }
}

public sealed record OciReferrerDescriptor(
    string MediaType,
    string Digest,
    long Size,
    string? ArtifactType,
    IReadOnlyDictionary<string, string> Annotations);
