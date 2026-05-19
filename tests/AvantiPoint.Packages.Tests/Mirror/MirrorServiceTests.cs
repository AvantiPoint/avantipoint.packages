using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests.Mirror;

public class MirrorServiceTests
{
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
            Mock.Of<ILogger<MirrorService>>());

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
            Mock.Of<ILogger<MirrorService>>());

        // Without a real upstream feed this returns NotFound; indexer must still never run for ProxyOnly attempts.
        await service.MirrorAsync(
            "Some.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        indexer.Verify(
            i => i.IndexAsync(It.IsAny<Stream>(), It.IsAny<PackageIngestionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
