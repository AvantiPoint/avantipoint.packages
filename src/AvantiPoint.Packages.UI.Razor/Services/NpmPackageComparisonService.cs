using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.UI.Services;

public sealed class NpmPackageComparisonService : INpmPackageComparisonService
{
    private readonly IContext _context;
    private readonly IFeedRegistry _feedRegistry;
    private readonly IMirrorPolicyService _policy;
    private readonly IPathBlobStore _blobStore;
    private readonly ILogger<NpmPackageComparisonService> _logger;

    public NpmPackageComparisonService(
        IContext context,
        IFeedRegistry feedRegistry,
        IMirrorPolicyService policy,
        IStorageBackendFactory storageFactory,
        ILogger<NpmPackageComparisonService> logger)
    {
        _context = context;
        _feedRegistry = feedRegistry;
        _policy = policy;
        _blobStore = storageFactory.CreatePathStore("npm/");
        _logger = logger;
    }

    public async Task<NpmPackageVersionComparison?> CompareAsync(
        string packageName,
        string baseVersion,
        string targetVersion,
        string? selectedFile = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName)
            || string.IsNullOrWhiteSpace(baseVersion)
            || string.IsNullOrWhiteSpace(targetVersion)
            || string.Equals(baseVersion, targetVersion, StringComparison.Ordinal))
        {
            return null;
        }

        var normalizedName = packageName.StartsWith('@')
            ? packageName
            : packageName.ToLowerInvariant();
        var feedId = _feedRegistry.Feed.FeedId;
        var versions = await _context.NpmVersions
            .AsNoTracking()
            .Where(version => version.FeedId == feedId
                && version.Package.Name == normalizedName
                && (version.Version == baseVersion || version.Version == targetVersion))
            .ToListAsync(cancellationToken);
        var baseEntity = versions.FirstOrDefault(version =>
            string.Equals(version.Version, baseVersion, StringComparison.Ordinal));
        var targetEntity = versions.FirstOrDefault(version =>
            string.Equals(version.Version, targetVersion, StringComparison.Ordinal));
        if (baseEntity is null
            || targetEntity is null
            || !_policy.IncludeInDiscovery(FeedProtocol.Npm, baseEntity.Origin)
            || !_policy.IncludeInDiscovery(FeedProtocol.Npm, targetEntity.Origin))
        {
            return null;
        }

        Stream? baseTarball = null;
        Stream? targetTarball = null;
        try
        {
            baseTarball = await OpenStoredTarballAsync(baseEntity.TarballPath, cancellationToken);
            targetTarball = await OpenStoredTarballAsync(targetEntity.TarballPath, cancellationToken);
            return await NpmPackageComparisonBuilder.CompareAsync(
                normalizedName,
                baseEntity.Version,
                baseEntity.PackumentJson,
                baseTarball,
                targetEntity.Version,
                targetEntity.PackumentJson,
                targetTarball,
                selectedFile,
                cancellationToken);
        }
        finally
        {
            if (baseTarball is not null)
            {
                await baseTarball.DisposeAsync();
            }

            if (targetTarball is not null)
            {
                await targetTarball.DisposeAsync();
            }
        }
    }

    private async Task<Stream?> OpenStoredTarballAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return await _blobStore.ExistsAsync(path, cancellationToken)
                ? await _blobStore.GetAsync(path, cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not open stored npm tarball {TarballPath} for comparison", path);
            return null;
        }
    }
}
