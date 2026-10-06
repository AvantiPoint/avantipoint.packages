using AvantiPoint.Packages.Registry.Native.Storage;

namespace AvantiPoint.Packages.Registry.Native.Maven;

public sealed record MavenArtifactPath(string Path, string PackageName, string? Version,
    string ArtifactId, string GroupId, string? ChecksumAlgorithm, bool IsMetadata)
{
    public static bool TryParse(string path, out MavenArtifactPath? artifact)
    {
        artifact = null;
        if (!ArtifactPath.IsValid(path)) return false;
        string? algorithm = null;
        foreach (var suffix in new[] { ".sha256", ".sha512", ".sha1", ".md5" })
            if (path.EndsWith(suffix, StringComparison.Ordinal))
            {
                algorithm = suffix[1..];
                path = path[..^suffix.Length];
                break;
            }
        var parts = path.Split('/');
        if (parts.Length < 3) return false;
        var metadata = parts[^1] == "maven-metadata.xml";
        if (!metadata && parts.Length < 4) return false;
        var artifactIndex = parts.Length - (metadata ? 2 : 3);
        var group = string.Join('.', parts[..artifactIndex]);
        var id = parts[artifactIndex];
        var version = metadata ? null : parts[^2];
        // Snapshot metadata and moving snapshot artifacts need a separate policy.
        if (version?.EndsWith("-SNAPSHOT", StringComparison.OrdinalIgnoreCase) == true) return false;
        if (!metadata)
        {
            var filename = parts[^1];
            var stem = id + "-" + version;
            if (!filename.StartsWith(stem + ".", StringComparison.Ordinal)
                && !filename.StartsWith(stem + "-", StringComparison.Ordinal)) return false;
            var unsigned = filename.EndsWith(".asc", StringComparison.Ordinal) ? filename[..^4] : filename;
            if (!new[] { ".pom", ".aar", ".jar", ".module" }.Any(e => unsigned.EndsWith(e, StringComparison.Ordinal)))
                return false;
        }
        if ((group + ":" + id).Length > 256 || version?.Length > 128) return false;
        artifact = new(path, group + ":" + id, version, id, group, algorithm, metadata);
        return true;
    }
}
