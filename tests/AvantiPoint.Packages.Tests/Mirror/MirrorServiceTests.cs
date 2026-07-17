using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests.Mirror;

public class MirrorServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsSuccessfulSourcesWhenAnotherSourceFails()
    {
        var successful = new PackageSource { Name = "successful", Priority = 1 };
        var failing = new PackageSource { Name = "failing", Priority = 2 };
        var packageSources = new Mock<IPackageSourceService>();
        packageSources
            .Setup(service => service.GetEnabledUpstreamSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([successful, failing]);
        var upstream = new StubPackageSourceSearchClient((source, _, _) =>
        {
            if (source == failing)
            {
                throw new HttpRequestException("unavailable");
            }

            return Task.FromResult(new SearchResponse
            {
                TotalHits = 1,
                Data = [new SearchResult { PackageId = "Example", Version = "1.0.0" }],
            });
        });
        var service = new MirrorService(
            Mock.Of<IPackageService>(),
            packageSources.Object,
            Mock.Of<IPackageIndexingService>(),
            Mock.Of<IPackageStorageService>(),
            Mock.Of<ILocalPackageCacheService>(),
            Mock.Of<ILogger<MirrorService>>(),
            new NullSecretProtector(),
            upstream);

        var results = await service.SearchAsync(
            new SearchRequest { Query = "Example", Take = 20 },
            TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("Example", Assert.Single(result.Data).PackageId);
        Assert.Equal(2, upstream.SearchCount);
    }

    [Fact]
    public async Task MirrorAsync_WhenPackageInStorage_ReturnsAlreadyAvailableWithoutIndexing()
    {
        var storageStream = new MemoryStream([1, 2, 3]);
        var storage = new Mock<IPackageStorageService>();
        storage.Setup(s => s.GetPackageStreamAsync(
                "Cached.Package",
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storageStream);

        var localPackages = new Mock<IPackageService>();
        localPackages.Setup(p => p.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var indexer = new Mock<IPackageIndexingService>();

        var service = new MirrorService(
            localPackages.Object,
            Mock.Of<IPackageSourceService>(),
            indexer.Object,
            storage.Object,
            Mock.Of<ILocalPackageCacheService>(),
            Mock.Of<ILogger<MirrorService>>(),
            new NullSecretProtector());

        var result = await service.MirrorAsync(
            "Cached.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.Equal(MirrorOperationResult.AlreadyAvailable, result);
        indexer.Verify(
            i => i.IndexAsync(It.IsAny<Stream>(), It.IsAny<PackageIngestionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MirrorAsync_ProxyOnly_DoesNotCallIndexer()
    {
        var source = new PackageSource
        {
            Id = 1,
            Name = "upstream",
            FeedUrl = "https://api.nuget.org/v3/index.json",
            CachingStrategy = PackageSourceCachingStrategy.ProxyOnly,
        };

        var packageSourceService = new Mock<IPackageSourceService>();
        packageSourceService.Setup(s => s.GetEnabledUpstreamSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PackageSource> { source });

        var localPackages = new Mock<IPackageService>();
        localPackages.Setup(p => p.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var indexer = new Mock<IPackageIndexingService>();

        var service = new MirrorService(
            localPackages.Object,
            packageSourceService.Object,
            indexer.Object,
            Mock.Of<IPackageStorageService>(),
            Mock.Of<ILocalPackageCacheService>(),
            Mock.Of<ILogger<MirrorService>>(),
            new NullSecretProtector());

        // Without a real upstream feed this returns NotFound; indexer must still never run for ProxyOnly attempts.
        await service.MirrorAsync(
            "Some.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        indexer.Verify(
            i => i.IndexAsync(It.IsAny<Stream>(), It.IsAny<PackageIngestionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MirrorAsync_WhenPackageExistsInLocalCache_StreamsWithoutCallingUpstream()
    {
        var packageContent = new MemoryStream([1, 2, 3]);
        var localCache = new Mock<ILocalPackageCacheService>();
        localCache.Setup(c => c.TryOpenPackageAsync(
                "Local.Package",
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalPackageCacheEntry("cache-path", packageContent, CopyToFeedStorage: false));

        var packageSources = new Mock<IPackageSourceService>(MockBehavior.Strict);
        var indexer = new Mock<IPackageIndexingService>(MockBehavior.Strict);
        var service = new MirrorService(
            CreateMissingLocalPackages(),
            packageSources.Object,
            indexer.Object,
            Mock.Of<IPackageStorageService>(),
            localCache.Object,
            Mock.Of<ILogger<MirrorService>>(),
            new NullSecretProtector());

        var result = await service.MirrorAsync(
            "Local.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.True(result.HasDirectStream);
        Assert.False(result.IsProxied);
        Assert.Same(packageContent, result.DirectStream);
        Assert.Null(result.Source);
        packageSources.VerifyNoOtherCalls();
        indexer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MirrorAsync_WhenLocalCacheCopyEnabled_UsesCacheOnlyIngestion()
    {
        var packageContent = new MemoryStream([1, 2, 3]);
        var localCache = new Mock<ILocalPackageCacheService>();
        localCache.Setup(c => c.TryOpenPackageAsync(
                "Local.Package",
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalPackageCacheEntry("cache-path", packageContent, CopyToFeedStorage: true));

        PackageIngestionContext? capturedContext = null;
        var indexer = new Mock<IPackageIndexingService>();
        indexer.Setup(i => i.IndexAsync(
                packageContent,
                It.IsAny<PackageIngestionContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<Stream, PackageIngestionContext, CancellationToken>((_, context, _) => capturedContext = context)
            .ReturnsAsync(new PackageIndexingResult { Status = PackageIndexingStatus.Success });

        var packageSources = new Mock<IPackageSourceService>(MockBehavior.Strict);
        var service = new MirrorService(
            CreateMissingLocalPackages(),
            packageSources.Object,
            indexer.Object,
            Mock.Of<IPackageStorageService>(),
            localCache.Object,
            Mock.Of<ILogger<MirrorService>>(),
            new NullSecretProtector());

        var result = await service.MirrorAsync(
            "Local.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.Equal(MirrorOperationResult.StoredFromLocalCache, result);
        Assert.NotNull(capturedContext);
        Assert.Equal(PackageOrigin.Cached, capturedContext.Origin);
        Assert.Equal(PackageSourceCachingStrategy.CacheOnly, capturedContext.CachingStrategy);
        Assert.True(capturedContext.SkipDatabasePersistence);
        Assert.True(capturedContext.SkipSearchIndexing);
        Assert.False(capturedContext.ApplyPublishSignaturePolicy);
        packageSources.VerifyNoOtherCalls();
    }

    private static IPackageService CreateMissingLocalPackages()
    {
        var packages = new Mock<IPackageService>();
        packages.Setup(p => p.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return packages.Object;
    }

    private sealed class StubPackageSourceSearchClient(
        Func<PackageSource, SearchRequest, CancellationToken, Task<SearchResponse>> search)
        : IPackageSourceSearchClient
    {
        private int _searchCount;

        public int SearchCount => _searchCount;

        public Task<SearchResponse> SearchAsync(
            PackageSource source,
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _searchCount);
            return search(source, request, cancellationToken);
        }
    }
}
