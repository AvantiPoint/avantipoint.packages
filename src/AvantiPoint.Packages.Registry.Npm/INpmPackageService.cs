using System.Text.Json.Nodes;

namespace AvantiPoint.Packages.Registry.Npm;

public interface INpmPackageService
{
    Task<JsonObject?> GetPackumentAsync(
        string feedId,
        string packageName,
        Uri publicBaseUrl,
        CancellationToken cancellationToken = default);

    Task<Stream?> GetTarballAsync(
        string feedId,
        string packageName,
        string tarballFileName,
        Uri publicBaseUrl,
        CancellationToken cancellationToken = default);

    Task PublishAsync(
        string feedId,
        string packageName,
        string version,
        Stream tarball,
        JsonObject versionMetadata,
        Uri publicBaseUrl,
        CancellationToken cancellationToken = default);

    Task<NpmSearchResult> SearchAsync(
        string feedId,
        string? query,
        int from,
        int size,
        CancellationToken cancellationToken = default);
}

public sealed record NpmSearchResult(int Total, IReadOnlyList<NpmSearchObject> Objects);

public sealed record NpmSearchObject(
    string Name,
    string Version,
    string? Description,
    DateTime? Published);
