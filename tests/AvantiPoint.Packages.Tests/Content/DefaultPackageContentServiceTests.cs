using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests.Content;

public class DefaultPackageContentServiceTests
{
    [Fact]
    public async Task GetPackageContentStreamOrNullAsync_WithSigningDisabled_ReturnsUpstreamPackageUnchanged()
    {
        var upstreamStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var mirror = new Mock<IMirrorService>();
        mirror.Setup(m => m.MirrorAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MirrorOperationResult.AlreadyAvailable);

        var package = new Package
        {
            Id = "Test.Package",
            Version = NuGetVersion.Parse("1.0.0"),
            Origin = PackageOrigin.Mirrored,
            PackageSourceId = 1,
            HasEmbeddedIcon = false,
            HasEmbeddedLicense = false,
            HasReadme = false,
            IsSigned = true
        };

        var packages = new Mock<IPackageService>();
        packages.Setup(p => p.AddDownloadAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        packages.Setup(p => p.FindOrNullAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var storage = new Mock<IPackageStorageService>();
        storage.Setup(s => s.GetPackageStreamAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(upstreamStream);
        storage.Setup(s => s.GetSignedPackageStreamOrNullAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stream.Null);

        var packageSourceService = new Mock<IPackageSourceService>();
        packageSourceService.Setup(s => s.GetRequiredAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageSource
            {
                Id = 1,
                MirrorSignaturePolicy = MirrorRepositorySignaturePolicy.Merge
            });

        var signingOptions = Options.Create(new SigningOptions());

        var service = new DefaultPackageContentService(
            mirror.Object,
            packages.Object,
            storage.Object,
            new NullSigningKeyProvider(),
            Mock.Of<IPackageSigningService>(),
            new PackageSignatureStripper(Mock.Of<ILogger<PackageSignatureStripper>>()),
            signingOptions,
            packageSourceService.Object,
            new RepositorySigningCertificateService(
                Mock.Of<IContext>(),
                Mock.Of<ILogger<RepositorySigningCertificateService>>(),
                TimeProvider.System,
                Mock.Of<IUrlGenerator>()),
            Mock.Of<ILogger<DefaultPackageContentService>>());

        var result = await service.GetPackageContentStreamOrNullAsync(package.Id, package.Version, TestContext.Current.CancellationToken);

        Assert.Same(upstreamStream, result);
        storage.Verify(s => s.GetSignedPackageStreamOrNullAsync(
            It.IsAny<string>(),
            It.IsAny<NuGetVersion>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPackageContentStreamOrNullAsync_WhenPackageNotFound_ReturnsNull()
    {
        var mirror = new Mock<IMirrorService>();
        mirror.Setup(m => m.MirrorAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MirrorOperationResult.AlreadyAvailable);

        var packages = new Mock<IPackageService>();
        packages.Setup(p => p.AddDownloadAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Package doesn't exist

        var storage = new Mock<IPackageStorageService>();
        storage.Setup(s => s.GetPackageStreamAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        var signingOptions = Options.Create(new SigningOptions());

        var service = new DefaultPackageContentService(
            mirror.Object,
            packages.Object,
            storage.Object,
            new NullSigningKeyProvider(),
            Mock.Of<IPackageSigningService>(),
            new PackageSignatureStripper(Mock.Of<ILogger<PackageSignatureStripper>>()),
            signingOptions,
            Mock.Of<IPackageSourceService>(),
            new RepositorySigningCertificateService(
                Mock.Of<IContext>(),
                Mock.Of<ILogger<RepositorySigningCertificateService>>(),
                TimeProvider.System,
                Mock.Of<IUrlGenerator>()),
            Mock.Of<ILogger<DefaultPackageContentService>>());

        var result = await service.GetPackageContentStreamOrNullAsync("NonExistent.Package", NuGetVersion.Parse("1.0.0"), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPackageContentStreamOrNullAsync_WithoutDatabaseRow_ReturnsStorageStream()
    {
        var cachedStream = new MemoryStream(new byte[] { 9, 8, 7 });

        var mirror = new Mock<IMirrorService>();
        mirror.Setup(m => m.MirrorAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MirrorOperationResult.AlreadyAvailable);

        var packages = new Mock<IPackageService>();
        packages.Setup(p => p.AddDownloadAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var storage = new Mock<IPackageStorageService>();
        storage.Setup(s => s.GetPackageStreamAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedStream);

        var signingOptions = Options.Create(new SigningOptions());

        var service = new DefaultPackageContentService(
            mirror.Object,
            packages.Object,
            storage.Object,
            new NullSigningKeyProvider(),
            Mock.Of<IPackageSigningService>(),
            new PackageSignatureStripper(Mock.Of<ILogger<PackageSignatureStripper>>()),
            signingOptions,
            Mock.Of<IPackageSourceService>(),
            new RepositorySigningCertificateService(
                Mock.Of<IContext>(),
                Mock.Of<ILogger<RepositorySigningCertificateService>>(),
                TimeProvider.System,
                Mock.Of<IUrlGenerator>()),
            Mock.Of<ILogger<DefaultPackageContentService>>());

        var result = await service.GetPackageContentStreamOrNullAsync(
            "Cached.Package",
            NuGetVersion.Parse("1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.Same(cachedStream, result);
    }

    [Fact]
    public async Task GetPackageVersionsOrNullAsync_WhenPackageNotFound_ReturnsNull()
    {
        var mirror = new Mock<IMirrorService>();
        mirror.Setup(m => m.FindPackageVersionsOrNullAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<NuGetVersion>?)null);

        var packages = new Mock<IPackageService>();
        packages.Setup(p => p.FindVersionsAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NuGetVersion>().ToList().AsReadOnly()); // No versions found

        var storage = new Mock<IPackageStorageService>();

        var signingOptions = Options.Create(new SigningOptions());

        var service = new DefaultPackageContentService(
            mirror.Object,
            packages.Object,
            storage.Object,
            new NullSigningKeyProvider(),
            Mock.Of<IPackageSigningService>(),
            new PackageSignatureStripper(Mock.Of<ILogger<PackageSignatureStripper>>()),
            signingOptions,
            Mock.Of<IPackageSourceService>(),
            new RepositorySigningCertificateService(
                Mock.Of<IContext>(),
                Mock.Of<ILogger<RepositorySigningCertificateService>>(),
                TimeProvider.System,
                Mock.Of<IUrlGenerator>()),
            Mock.Of<ILogger<DefaultPackageContentService>>());

        var result = await service.GetPackageVersionsOrNullAsync("NonExistent.Package", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPackageVersionsOrNullAsync_WhenPackageExists_ReturnsVersions()
    {
        var versions = new[] { NuGetVersion.Parse("1.0.0"), NuGetVersion.Parse("2.0.0") };

        var mirror = new Mock<IMirrorService>();
        mirror.Setup(m => m.FindPackageVersionsOrNullAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<NuGetVersion>?)null);

        var packages = new Mock<IPackageService>();
        packages.Setup(p => p.FindVersionsAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions.ToList().AsReadOnly());

        var storage = new Mock<IPackageStorageService>();

        var signingOptions = Options.Create(new SigningOptions());

        var service = new DefaultPackageContentService(
            mirror.Object,
            packages.Object,
            storage.Object,
            new NullSigningKeyProvider(),
            Mock.Of<IPackageSigningService>(),
            new PackageSignatureStripper(Mock.Of<ILogger<PackageSignatureStripper>>()),
            signingOptions,
            Mock.Of<IPackageSourceService>(),
            new RepositorySigningCertificateService(
                Mock.Of<IContext>(),
                Mock.Of<ILogger<RepositorySigningCertificateService>>(),
                TimeProvider.System,
                Mock.Of<IUrlGenerator>()),
            Mock.Of<ILogger<DefaultPackageContentService>>());

        var result = await service.GetPackageVersionsOrNullAsync("Test.Package", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.Versions.Count);
        Assert.Contains("1.0.0", result.Versions);
        Assert.Contains("2.0.0", result.Versions);
    }
}
