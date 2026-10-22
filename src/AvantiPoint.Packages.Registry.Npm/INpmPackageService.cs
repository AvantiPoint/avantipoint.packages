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
        CancellationToken cancellationToken = default,
        string distTag = "latest");

    Task SetDistTagAsync(string feedId, string packageName, string tag, string? version, CancellationToken cancellationToken = default);

    Task UpdateDeprecationsAsync(string feedId, string packageName, JsonObject packument, CancellationToken cancellationToken = default);

    Task UnpublishAsync(string feedId, string packageName, string revision, JsonObject? remainingPackument, CancellationToken cancellationToken = default);

    Task ConfirmTarballRemovalAsync(string feedId, string packageName, string tarballFileName, string revision, CancellationToken cancellationToken = default);

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
