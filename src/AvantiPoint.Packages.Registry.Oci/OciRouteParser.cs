using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Packages.Registry.Oci;

public enum OciRouteKind
{
    Version,
    Catalog,
    Manifest,
    Blob,
    StartUpload,
    UploadSession,
    ListTags,
}

public sealed record OciRoute(
    OciRouteKind Kind,
    string? RepositoryName,
    string? Reference,
    string? Digest,
    string? UploadId);

public static class OciRouteParser
{
    public static bool TryParse(PathString path, out OciRoute route)
    {
        route = null!;
        var value = path.Value ?? string.Empty;
        if (!value.StartsWith("/v2", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = value.Length == 3 ? string.Empty : value[3..].TrimStart('/');
        if (string.IsNullOrEmpty(remainder))
        {
            route = new OciRoute(OciRouteKind.Version, null, null, null, null);
            return true;
        }

        if (remainder.Equals("_catalog", StringComparison.OrdinalIgnoreCase))
        {
            route = new OciRoute(OciRouteKind.Catalog, null, null, null, null);
            return true;
        }

        if (TryMatchSuffix(remainder, "/manifests/", out var repository, out var reference))
        {
            route = new OciRoute(OciRouteKind.Manifest, repository, reference, null, null);
            return true;
        }

        if (TryMatchSuffix(remainder, "/blobs/uploads/", out repository, out var uploadId)
            && !string.IsNullOrEmpty(uploadId))
        {
            route = new OciRoute(OciRouteKind.UploadSession, repository, null, null, uploadId.TrimEnd('/'));
            return true;
        }

        if (TryMatchSuffix(remainder, "/blobs/uploads", out repository, out _)
            || TryMatchSuffix(remainder, "/blobs/uploads/", out repository, out _))
        {
            route = new OciRoute(OciRouteKind.StartUpload, repository, null, null, null);
            return true;
        }

        if (TryMatchSuffix(remainder, "/blobs/", out repository, out var digest))
        {
            route = new OciRoute(OciRouteKind.Blob, repository, null, digest, null);
            return true;
        }

        if (TryMatchSuffix(remainder, "/tags/list", out repository, out _))
        {
            route = new OciRoute(OciRouteKind.ListTags, repository, null, null, null);
            return true;
        }

        return false;
    }

    private static bool TryMatchSuffix(
        string path,
        string suffix,
        out string repository,
        out string? trailing)
    {
        repository = string.Empty;
        trailing = null;

        var index = path.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        repository = path[..index];
        trailing = path[(index + suffix.Length)..];
        return !string.IsNullOrEmpty(repository);
    }
}
