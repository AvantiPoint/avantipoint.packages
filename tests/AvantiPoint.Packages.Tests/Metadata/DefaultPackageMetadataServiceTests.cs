using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests.Metadata;

public class DefaultPackageMetadataServiceTests
{
    [Fact]
    public async Task GetRegistrationIndexOrNullAsync_ExcludeMirrored_DoesNotMergeUpstreamMetadata()
    {
        var upstreamPackage = new Package
        {
            Id = "Upstream.Only",
            Version = NuGetVersion.Parse("1.0.0"),
            Listed = true,
            Published = DateTime.UtcNow,
        };

        var mirror = new Mock<IMirrorService>();
        mirror.Setup(m => m.FindPackagesOrNullAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Package> { upstreamPackage });

        var packages = new Mock<IPackageService>();
        packages.Setup(p => p.FindAsync(
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Package>());

        var service = new DefaultPackageMetadataService(
            Mock.Of<IContext>(),
            mirror.Object,
            packages.Object,
            new RegistrationBuilder(Mock.Of<IUrlGenerator>()),
            Mock.Of<IUrlGenerator>(),
            Options.Create(new SearchOptions { IncludeMirroredPackages = false }),
            new DefaultFeedScope());

        var result = await service.GetRegistrationIndexOrNullAsync(
            "Upstream.Only",
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        mirror.Verify(
            m => m.FindPackagesOrNullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
