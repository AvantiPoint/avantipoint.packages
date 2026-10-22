using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;

namespace AvantiPoint.Packages.Registry.Oci;

public static class OciManifestParser
{
    private const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";
    private const string OciManifest = "application/vnd.oci.image.manifest.v1+json";
    private const string OciIndex = "application/vnd.oci.image.index.v1+json";
    private const string HelmConfig = "application/vnd.cncf.helm.config.v1+json";

    public static ParsedOciManifest Parse(string mediaType, ReadOnlySpan<byte> content)
    {
        try { return ParseCore(mediaType, content); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or FormatException)
        {
            throw new JsonException("The OCI manifest contains an invalid descriptor or field type.", exception);
        }
    }

    private static ParsedOciManifest ParseCore(string mediaType, ReadOnlySpan<byte> content)
    {
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(content));
        var root = document.RootElement;
        var normalizedMediaType = NormalizeMediaType(mediaType);
        var artifactType = ReadString(root, "artifactType") ?? ReadConfigMediaType(root);
        var subject = ReadDescriptor(root, "subject");
        if (root.TryGetProperty("subject", out _) && subject is null)
        {
            throw new JsonException("The OCI subject descriptor is invalid.");
        }
        var annotations = ReadStringMap(root, "annotations");

        var artifactKind = ResolveArtifactKind(normalizedMediaType, root);
        string? platformOs = null;
        string? platformArch = null;

        if (artifactKind is OciArtifactKind.Image or OciArtifactKind.Helm)
        {
            if (root.TryGetProperty("config", out var config)
                && config.TryGetProperty("digest", out _))
            {
                // Docker/OCI image manifest
            }

            if (root.TryGetProperty("config", out var configMediaType)
                && configMediaType.ValueKind == JsonValueKind.Object
                && configMediaType.TryGetProperty("mediaType", out var configType)
                && configType.GetString() == HelmConfig)
            {
                artifactKind = OciArtifactKind.Helm;
            }
        }

        if (root.TryGetProperty("manifests", out _))
        {
            artifactKind = OciArtifactKind.Index;
        }
        else if (subject is not null && artifactKind == OciArtifactKind.Image)
        {
            // OCI artifacts use the image-manifest envelope, but a signature, SBOM, or
            // report attached through subject is not itself a runnable container image.
            artifactKind = OciArtifactKind.Unknown;
        }

        if (root.TryGetProperty("platform", out var platform)
            && platform.ValueKind == JsonValueKind.Object)
        {
            if (platform.TryGetProperty("os", out var os))
            {
                platformOs = os.GetString();
            }

            if (platform.TryGetProperty("architecture", out var arch))
            {
                platformArch = arch.GetString();
            }
        }

        var referencedDigests = ExtractReferencedDigests(root);
        return new ParsedOciManifest(
            normalizedMediaType,
            artifactKind,
            platformOs,
            platformArch,
            referencedDigests,
            artifactType,
            subject,
            annotations);
    }

    public static bool IsAllowedMediaType(string mediaType, bool allowUnknownMediaTypes)
    {
        if (allowUnknownMediaTypes)
        {
            return true;
        }

        return NormalizeMediaType(mediaType) is DockerManifestV2
            or DockerManifestList
            or OciManifest
            or OciIndex
            or "application/vnd.cncf.helm.chart.content.v1.tar+gzip";
    }

    private static string NormalizeMediaType(string mediaType)
    {
        var parameterStart = mediaType.IndexOf(';');
        return (parameterStart < 0 ? mediaType : mediaType[..parameterStart]).Trim();
    }

    private static OciArtifactKind ResolveArtifactKind(string mediaType, JsonElement root)
    {
        if (mediaType is DockerManifestList or OciIndex || root.TryGetProperty("manifests", out _))
        {
            return OciArtifactKind.Index;
        }

        if (mediaType.Contains("helm", StringComparison.OrdinalIgnoreCase))
        {
            return OciArtifactKind.Helm;
        }

        if (mediaType is DockerManifestV2 or OciManifest)
        {
            return OciArtifactKind.Image;
        }

        return OciArtifactKind.Unknown;
    }

    private static IReadOnlyList<string> ExtractReferencedDigests(JsonElement root)
    {
        var digests = new List<string>();

        if (root.TryGetProperty("config", out var config)
            && config.TryGetProperty("digest", out var configDigest))
        {
            digests.Add(configDigest.GetString()!);
        }

        if (root.TryGetProperty("layers", out var layers))
        {
            foreach (var layer in layers.EnumerateArray())
            {
                if (layer.TryGetProperty("digest", out var digest))
                {
                    digests.Add(digest.GetString()!);
                }
            }
        }

        if (root.TryGetProperty("manifests", out var manifests))
        {
            foreach (var manifest in manifests.EnumerateArray())
            {
                if (manifest.TryGetProperty("digest", out var digest))
                {
                    digests.Add(digest.GetString()!);
                }
            }
        }

        return digests;
    }

    private static OciSubjectDescriptor? ReadDescriptor(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var descriptor)
            || descriptor.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var mediaType = ReadString(descriptor, "mediaType");
        var digest = ReadString(descriptor, "digest");
        if (string.IsNullOrWhiteSpace(mediaType)
            || !IsValidSubjectDigest(digest)
            || !descriptor.TryGetProperty("size", out var sizeElement)
            || !sizeElement.TryGetInt64(out var size)
            || size < 0)
        {
            return null;
        }

        return new OciSubjectDescriptor(mediaType, digest, size);
    }

    private static bool IsValidSubjectDigest([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? digest)
    {
        // The subject may not exist yet, so validate its content address without
        // relying on a successful repository lookup. Never permit paths or tag text.
        var hex = digest switch
        {
            { Length: 71 } when digest.StartsWith("sha256:", StringComparison.Ordinal) => digest.AsSpan(7),
            { Length: 135 } when digest.StartsWith("sha512:", StringComparison.Ordinal) => digest.AsSpan(7),
            _ => default,
        };
        return !hex.IsEmpty && hex.IndexOfAnyExcept("0123456789abcdef".AsSpan()) < 0;
    }

    private static string? ReadConfigMediaType(JsonElement root) =>
        root.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object
            ? ReadString(config, "mediaType")
            : null;

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

public sealed record ParsedOciManifest(
    string MediaType,
    OciArtifactKind ArtifactKind,
    string? PlatformOs,
    string? PlatformArch,
    IReadOnlyList<string> ReferencedDigests,
    string? ArtifactType,
    OciSubjectDescriptor? Subject,
    IReadOnlyDictionary<string, string> Annotations);

public sealed record OciSubjectDescriptor(
    string MediaType,
    string Digest,
    long Size);
