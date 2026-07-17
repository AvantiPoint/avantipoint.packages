using AvantiPoint.Packages.Integration.Tests.TestInfrastructure;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Integration.Tests;

public sealed class LocalPackageCacheIntegrationTests
{
    [Fact]
    public async Task PackageDownload_WhenOnlyLocalCacheContainsPackage_StreamsWithoutPersisting()
    {
        var cacheRoot = CreateCacheRoot();
        const string packageId = "Local.Cache.Package";
        const string version = "1.2.3";

        try
        {
            var packageBytes = TestPackageBuilder.CreatePackage(packageId, version);
            WritePackageToGlobalCache(cacheRoot, packageId, version, packageBytes);

            await using var host = await FeedUnderTestHost.StartAsync(
                new FeedUnderTestOptions
                {
                    IncludeMirroredPackages = false,
                    LocalCachePath = cacheRoot,
                    CopyLocalCacheToFeedStorage = false,
                },
                TestContext.Current.CancellationToken);

            var response = await host.Client.GetAsync(
                TestPackageBuilder.GetPackageDownloadPath(packageId, version),
                TestContext.Current.CancellationToken);

            response.EnsureSuccessStatusCode();
            Assert.Equal(
                packageBytes,
                await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.Null(await host.FindPackageAsync(
                packageId,
                version,
                TestContext.Current.CancellationToken));
            Assert.Equal(0, FeedUnderTestHost.CountPackageFiles(host.StoragePath));
        }
        finally
        {
            DeleteDirectory(cacheRoot);
        }
    }

    [Fact]
    public async Task PackageDownload_WhenLocalCacheCopyEnabled_PersistsWithoutDatabaseRow()
    {
        var cacheRoot = CreateCacheRoot();
        const string packageId = "Copied.Cache.Package";
        const string version = "2.0.0";

        try
        {
            var packageBytes = TestPackageBuilder.CreatePackage(packageId, version);
            WritePackageToGlobalCache(cacheRoot, packageId, version, packageBytes);

            await using var host = await FeedUnderTestHost.StartAsync(
                new FeedUnderTestOptions
                {
                    IncludeMirroredPackages = false,
                    LocalCachePath = cacheRoot,
                    CopyLocalCacheToFeedStorage = true,
                },
                TestContext.Current.CancellationToken);

            var response = await host.Client.GetAsync(
                TestPackageBuilder.GetPackageDownloadPath(packageId, version),
                TestContext.Current.CancellationToken);

            response.EnsureSuccessStatusCode();
            Assert.Null(await host.FindPackageAsync(
                packageId,
                version,
                TestContext.Current.CancellationToken));
            Assert.Equal(1, FeedUnderTestHost.CountPackageFiles(host.StoragePath));
        }
        finally
        {
            DeleteDirectory(cacheRoot);
        }
    }

    private static string CreateCacheRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "avantipoint-packages-global-cache-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WritePackageToGlobalCache(
        string cacheRoot,
        string packageId,
        string version,
        byte[] packageBytes)
    {
        var normalizedId = packageId.ToLowerInvariant();
        var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        var packageDirectory = Path.Combine(cacheRoot, normalizedId, normalizedVersion);
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllBytes(
            Path.Combine(packageDirectory, $"{normalizedId}.{normalizedVersion}.nupkg"),
            packageBytes);
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for test artifacts.
        }
    }
}
