using System.Text.Json.Nodes;

namespace AvantiPoint.Packages.Registry.Npm;

public interface INpmPackageService
{
    Task<JsonObject?> GetPackumentAsync(string feedId, string packageName, CancellationToken cancellationToken = default);

    Task<Stream?> GetTarballAsync(string feedId, string packageName, string tarballFileName, CancellationToken cancellationToken = default);

    Task PublishAsync(
        string feedId,
        string packageName,
        string version,
        Stream tarball,
        JsonObject versionMetadata,
        CancellationToken cancellationToken = default);
}
