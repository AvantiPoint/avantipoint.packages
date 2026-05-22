using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Integration.Tests.TestInfrastructure;
namespace AvantiPoint.Packages.Integration.Tests;

/// <summary>
/// Optional live-upstream checks against nuget.org (skipped when outbound network is unavailable).
/// </summary>
public sealed class NuGetOrgCachingIntegrationTests
{
    private const string PackageId = "Newtonsoft.Json";
    private const string Version = "13.0.3";

    [ExternalNetworkFact]
    public async Task NuGetOrg_IndexAndCache_PersistsDatabaseAndStorage()
    {
        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions
        {
            UpstreamServiceIndexUrl = ExternalNetworkFactAttribute.NuGetOrgServiceIndex,
            UpstreamSourceName = "nuget.org",
            CachingStrategy = PackageSourceCachingStrategy.IndexAndCache,
        });

        var download = await feed.Client.GetAsync(
            TestPackageBuilder.GetPackageDownloadPath(PackageId, Version),
            TestContext.Current.CancellationToken);
        download.EnsureSuccessStatusCode();

        var package = await feed.FindPackageAsync(PackageId, Version);
        Assert.NotNull(package);
        Assert.Equal(PackageOrigin.Mirrored, package.Origin);
        Assert.True(FeedUnderTestHost.CountPackageFiles(feed.StoragePath) >= 1);
    }

    [ExternalNetworkFact]
    public async Task NuGetOrg_CacheOnly_PersistsStorageOnly()
    {
        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions
        {
            UpstreamServiceIndexUrl = ExternalNetworkFactAttribute.NuGetOrgServiceIndex,
            UpstreamSourceName = "nuget.org",
            CachingStrategy = PackageSourceCachingStrategy.CacheOnly,
        });

        var download = await feed.Client.GetAsync(
            TestPackageBuilder.GetPackageDownloadPath(PackageId, Version),
            TestContext.Current.CancellationToken);
        download.EnsureSuccessStatusCode();

        Assert.Null(await feed.FindPackageAsync(PackageId, Version));
        Assert.True(FeedUnderTestHost.CountPackageFiles(feed.StoragePath) >= 1);
    }

    [ExternalNetworkFact]
    public async Task NuGetOrg_ProxyOnly_DoesNotPersistLocally()
    {
        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions
        {
            UpstreamServiceIndexUrl = ExternalNetworkFactAttribute.NuGetOrgServiceIndex,
            UpstreamSourceName = "nuget.org",
            CachingStrategy = PackageSourceCachingStrategy.ProxyOnly,
        });

        var filesBefore = FeedUnderTestHost.CountPackageFiles(feed.StoragePath);

        var download = await feed.Client.GetAsync(
            TestPackageBuilder.GetPackageDownloadPath(PackageId, Version),
            TestContext.Current.CancellationToken);
        download.EnsureSuccessStatusCode();

        Assert.Null(await feed.FindPackageAsync(PackageId, Version));
        Assert.Equal(filesBefore, FeedUnderTestHost.CountPackageFiles(feed.StoragePath));
    }
}
