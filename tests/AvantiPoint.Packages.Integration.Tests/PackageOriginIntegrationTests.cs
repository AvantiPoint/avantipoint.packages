using System.Net.Http.Json;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Integration.Tests.TestInfrastructure;
using AvantiPoint.Packages.Protocol.Models;
namespace AvantiPoint.Packages.Integration.Tests;

/// <summary>
/// End-to-end origin and caching-strategy coverage using OpenFeed as upstream (issue #581).
/// </summary>
public sealed class PackageOriginIntegrationTests
{
    private const string UpstreamPackageId = "OriginTest.Upstream";
    private const string PublishedPackageId = "OriginTest.Published";
    private const string Version = "1.0.0";

    [Fact]
    public async Task Publish_ToFeedUnderTest_SetsOriginPublished()
    {
        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions());

        await TestPackageBuilder.PublishAsync(feed.Client, PublishedPackageId, Version);

        var package = await feed.FindPackageAsync(PublishedPackageId, Version);
        Assert.NotNull(package);
        Assert.Equal(PackageOrigin.Published, package.Origin);
        Assert.True(FeedUnderTestHost.CountPackageFiles(feed.StoragePath) >= 1);
    }

    [Fact]
    public async Task Mirror_IndexAndCache_SetsOriginMirrored_AndAppearsInSearch()
    {
        await using var upstream = await UpstreamOpenFeedHost.StartAsync();
        await upstream.SeedPackageAsync(UpstreamPackageId, Version);

        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions
        {
            UpstreamServiceIndexUrl = upstream.ServiceIndexUrl,
            CachingStrategy = PackageSourceCachingStrategy.IndexAndCache,
            IncludeMirroredPackages = true,
        });

        var download = await feed.Client.GetAsync(
            TestPackageBuilder.GetPackageDownloadPath(UpstreamPackageId, Version),
            TestContext.Current.CancellationToken);
        download.EnsureSuccessStatusCode();

        var package = await feed.FindPackageAsync(UpstreamPackageId, Version);
        Assert.NotNull(package);
        Assert.Equal(PackageOrigin.Mirrored, package.Origin);
        Assert.True(FeedUnderTestHost.CountPackageFiles(feed.StoragePath) >= 1);

        var search = await feed.Client.GetAsync(
            $"/v3/search?q={UpstreamPackageId}&take=10&prerelease=true",
            TestContext.Current.CancellationToken);
        search.EnsureSuccessStatusCode();
        var searchResponse = await search.Content.ReadFromJsonAsync<SearchResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(searchResponse);
        Assert.Contains(searchResponse.Data, p => p.PackageId == UpstreamPackageId);
    }

    [Fact]
    public async Task Search_IncludeMirroredPackagesFalse_ExcludesMirrored_FromHttpAndDbDiscovery()
    {
        await using var upstream = await UpstreamOpenFeedHost.StartAsync();
        await upstream.SeedPackageAsync(UpstreamPackageId, Version);

        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions
        {
            UpstreamServiceIndexUrl = upstream.ServiceIndexUrl,
            CachingStrategy = PackageSourceCachingStrategy.IndexAndCache,
            IncludeMirroredPackages = false,
        });

        await TestPackageBuilder.PublishAsync(feed.Client, PublishedPackageId, Version);

        var download = await feed.Client.GetAsync(
            TestPackageBuilder.GetPackageDownloadPath(UpstreamPackageId, Version),
            TestContext.Current.CancellationToken);
        download.EnsureSuccessStatusCode();

        var mirrored = await feed.FindPackageAsync(UpstreamPackageId, Version);
        Assert.NotNull(mirrored);
        Assert.Equal(PackageOrigin.Mirrored, mirrored.Origin);

        var search = await feed.Client.GetAsync(
            "/v3/search?q=OriginTest&take=20&prerelease=true",
            TestContext.Current.CancellationToken);
        search.EnsureSuccessStatusCode();
        var searchResponse = await search.Content.ReadFromJsonAsync<SearchResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(searchResponse);
        Assert.Contains(searchResponse.Data, p => p.PackageId == PublishedPackageId);
        Assert.DoesNotContain(searchResponse.Data, p => p.PackageId == UpstreamPackageId);
    }

    [Fact]
    public async Task Mirror_CacheOnly_PersistsContent_SkipsDatabase_AndExcludesFromSearch()
    {
        await using var upstream = await UpstreamOpenFeedHost.StartAsync();
        await upstream.SeedPackageAsync(UpstreamPackageId, Version);

        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions
        {
            UpstreamServiceIndexUrl = upstream.ServiceIndexUrl,
            CachingStrategy = PackageSourceCachingStrategy.CacheOnly,
            IncludeMirroredPackages = true,
        });

        var download = await feed.Client.GetAsync(
            TestPackageBuilder.GetPackageDownloadPath(UpstreamPackageId, Version),
            TestContext.Current.CancellationToken);
        download.EnsureSuccessStatusCode();
        Assert.True(download.Content.Headers.ContentLength > 0);

        var package = await feed.FindPackageAsync(UpstreamPackageId, Version);
        Assert.Null(package);
        Assert.True(FeedUnderTestHost.CountPackageFiles(feed.StoragePath) >= 1);

        var search = await feed.Client.GetAsync(
            $"/v3/search?q={UpstreamPackageId}&take=10&prerelease=true",
            TestContext.Current.CancellationToken);
        search.EnsureSuccessStatusCode();
        var searchResponse = await search.Content.ReadFromJsonAsync<SearchResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(searchResponse);
        Assert.DoesNotContain(searchResponse.Data, p => p.PackageId == UpstreamPackageId);
    }

    [Fact]
    public async Task Mirror_ProxyOnly_DoesNotPersistContent_OrDatabase()
    {
        await using var upstream = await UpstreamOpenFeedHost.StartAsync();
        await upstream.SeedPackageAsync(UpstreamPackageId, Version);

        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions
        {
            UpstreamServiceIndexUrl = upstream.ServiceIndexUrl,
            CachingStrategy = PackageSourceCachingStrategy.ProxyOnly,
        });

        var filesBefore = FeedUnderTestHost.CountPackageFiles(feed.StoragePath);

        var download = await feed.Client.GetAsync(
            TestPackageBuilder.GetPackageDownloadPath(UpstreamPackageId, Version),
            TestContext.Current.CancellationToken);
        download.EnsureSuccessStatusCode();
        Assert.True(download.Content.Headers.ContentLength > 0);

        var package = await feed.FindPackageAsync(UpstreamPackageId, Version);
        Assert.Null(package);
        Assert.Equal(filesBefore, FeedUnderTestHost.CountPackageFiles(feed.StoragePath));
    }
}
