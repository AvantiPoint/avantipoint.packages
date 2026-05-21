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
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(content));
        var root = document.RootElement;

        var artifactKind = ResolveArtifactKind(mediaType, root);
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

        if (root.TryGetProperty("platform", out var platform))
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
            mediaType,
            artifactKind,
            platformOs,
            platformArch,
            referencedDigests);
    }

    public static bool IsAllowedMediaType(string mediaType, bool allowUnknownMediaTypes)
    {
        if (allowUnknownMediaTypes)
        {
            return true;
        }

        return mediaType is DockerManifestV2
            or DockerManifestList
            or OciManifest
            or OciIndex
            or "application/vnd.cncf.helm.chart.content.v1.tar+gzip";
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
}

public sealed record ParsedOciManifest(
    string MediaType,
    OciArtifactKind ArtifactKind,
    string? PlatformOs,
    string? PlatformArch,
    IReadOnlyList<string> ReferencedDigests);
