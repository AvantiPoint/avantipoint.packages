#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Reads the standard NuGet global packages folder layout without mutating it.
/// </summary>
public sealed class LocalPackageCacheService(
    IOptions<LocalCacheOptions> options,
    ILogger<LocalPackageCacheService> logger) : ILocalPackageCacheService
{
    private readonly LocalCacheOptions _options = options.Value;

    public Task<LocalPackageCacheEntry?> TryOpenPackageAsync(
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentNullException.ThrowIfNull(version);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
        {
            return Task.FromResult<LocalPackageCacheEntry?>(null);
        }

        string rootPath;
        string packagePath;
        string normalizedVersion;
        try
        {
            var resolvedRootPath = ResolveRootPath();
            if (resolvedRootPath is null)
            {
                logger.LogWarning(
                    "The local NuGet cache is enabled, but a global packages folder could not be resolved");
                return Task.FromResult<LocalPackageCacheEntry?>(null);
            }

            rootPath = resolvedRootPath;

            var normalizedId = packageId.ToLowerInvariant();
            normalizedVersion = version.ToNormalizedString().ToLowerInvariant();
            packagePath = Path.GetFullPath(Path.Combine(
                rootPath,
                normalizedId,
                normalizedVersion,
                $"{normalizedId}.{normalizedVersion}.nupkg"));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            logger.LogWarning(
                exception,
                "Unable to resolve a local cache path for package {PackageId} {PackageVersion}",
                packageId,
                version.ToNormalizedString());
            return Task.FromResult<LocalPackageCacheEntry?>(null);
        }

        if (!IsWithinRoot(rootPath, packagePath))
        {
            logger.LogWarning(
                "Rejected local cache path outside the configured root for package {PackageId} {PackageVersion}",
                packageId,
                normalizedVersion);
            return Task.FromResult<LocalPackageCacheEntry?>(null);
        }

        try
        {
            var stream = new FileStream(packagePath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });

            return Task.FromResult<LocalPackageCacheEntry?>(new(
                packagePath,
                stream,
                _options.CopyToFeedStorage));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<LocalPackageCacheEntry?>(null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Unable to read package {PackageId} {PackageVersion} from the local NuGet cache",
                packageId,
                normalizedVersion);
            return Task.FromResult<LocalPackageCacheEntry?>(null);
        }
    }

    private string? ResolveRootPath()
    {
        var configuredPath = string.IsNullOrWhiteSpace(_options.Path)
            ? Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            : _options.Path;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userProfile))
            {
                return null;
            }

            configuredPath = Path.Combine(userProfile, ".nuget", "packages");
        }

        configuredPath = Environment.ExpandEnvironmentVariables(configuredPath);
        if (configuredPath == "~")
        {
            configuredPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else if (configuredPath.StartsWith("~/", StringComparison.Ordinal) ||
                 configuredPath.StartsWith("~\\", StringComparison.Ordinal))
        {
            configuredPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                configuredPath[2..]);
        }

        return Path.GetFullPath(configuredPath);
    }

    private static bool IsWithinRoot(string rootPath, string packagePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, packagePath);
        return !Path.IsPathRooted(relativePath) &&
               relativePath != ".." &&
               !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
