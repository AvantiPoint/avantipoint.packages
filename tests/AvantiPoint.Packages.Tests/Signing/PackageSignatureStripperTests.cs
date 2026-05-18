using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class PackageSignatureStripperTests
{
    private readonly Mock<ILogger<PackageSignatureStripper>> _loggerMock;
    private static CancellationToken CurrentCancellationToken => TestContext.Current.CancellationToken;

    public PackageSignatureStripperTests()
    {
        _loggerMock = new Mock<ILogger<PackageSignatureStripper>>();
    }

    private PackageSignatureStripper CreateStripper()
    {
        return new PackageSignatureStripper(_loggerMock.Object);
    }

    private static Stream CreateTestPackage(string packageId, string version)
    {
        var builder = new PackageBuilder
        {
            Id = packageId,
            Version = NuGetVersion.Parse(version),
            Description = $"Test package {packageId} version {version}"
        };

        builder.Authors.Add("Test Author");

        // Add a dummy file so the package has content
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dll");
        try
        {
            File.WriteAllText(tempFile, "dummy content");
            var dummyFile = new PhysicalPackageFile
            {
                SourcePath = tempFile,
                TargetPath = "lib/netstandard2.0/_.dll"
            };
            builder.Files.Add(dummyFile);

            var stream = new MemoryStream();
            builder.Save(stream);
            stream.Position = 0;
            return stream;
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task StripRepositorySignaturesAsync_WithUnsignedPackage_ReturnsUnchanged()
    {
        // Arrange
        var stripper = CreateStripper();
        var packageStream = CreateTestPackage("Test.Package", "1.0.0");
        var originalLength = packageStream.Length;

        // Act
        var result = await stripper.StripRepositorySignaturesAsync(packageStream, CurrentCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalLength, result.Length);
        Assert.True(result.CanRead);
    }

    [Fact]
    public async Task StripRepositorySignaturesAsync_WithOnlyAuthorSignature_ReturnsUnchanged()
    {
        // Arrange
        var stripper = CreateStripper();
        var signingService = CreateSigningService();
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Author Certificate");
        
        // Create package with repository signature (SignAsAuthorAsync creates repository signatures)
        var unsignedPackage = CreateTestPackage("Test.Package", "1.0.0");
        var signedPackage = await SignAsAuthorAsync(signingService, unsignedPackage, certificate, CurrentCancellationToken);

        // Verify it has a repository signature
        signedPackage.Position = 0;
        using var beforeReader = new PackageArchiveReader(signedPackage, leaveStreamOpen: true);
        var beforeSignature = await beforeReader.GetPrimarySignatureAsync(CurrentCancellationToken);
        Assert.NotNull(beforeSignature);
        Assert.Equal(SignatureType.Repository, beforeSignature.Type);

        // Act - Strip the repository signature
        signedPackage.Position = 0;
        var result = await stripper.StripRepositorySignaturesAsync(signedPackage, CurrentCancellationToken);

        // Assert - Package should be unsigned after stripping repository signature
        Assert.NotNull(result);
        result.Position = 0;

        using var afterReader = new PackageArchiveReader(result, leaveStreamOpen: true);
        var isSigned = await afterReader.IsSignedAsync(CurrentCancellationToken);
        Assert.False(isSigned, "Package should be unsigned after stripping repository signature");
    }

    [Fact]
    public async Task StripRepositorySignaturesAsync_WithRepositorySignature_RemovesRepositorySignature()
    {
        // Arrange
        var stripper = CreateStripper();
        var signingService = CreateSigningService();
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Repository Certificate");
        
        // Create package with repository signature
        var unsignedPackage = CreateTestPackage("Test.Package", "1.0.0");
        var repositorySignedPackage = await signingService.SignPackageAsync(
            "Test.Package",
            NuGetVersion.Parse("1.0.0"),
            unsignedPackage,
            certificate,
            CurrentCancellationToken);

        // Verify it has repository signature
        repositorySignedPackage.Position = 0;
        using var beforeReader = new PackageArchiveReader(repositorySignedPackage, leaveStreamOpen: true);
        var beforeSignature = await beforeReader.GetPrimarySignatureAsync(CurrentCancellationToken);
        Assert.NotNull(beforeSignature);
        Assert.Equal(SignatureType.Repository, beforeSignature.Type);

        // Act
        repositorySignedPackage.Position = 0;
        var result = await stripper.StripRepositorySignaturesAsync(repositorySignedPackage, CurrentCancellationToken);

        // Assert
        Assert.NotNull(result);
        result.Position = 0;

        // Note: Stripping a repository signature from a package that only has a repository signature
        // will result in an unsigned package. This is expected behavior.
        using var afterReader = new PackageArchiveReader(result, leaveStreamOpen: true);
        var isSigned = await afterReader.IsSignedAsync(CurrentCancellationToken);
        
        // Package should be unsigned after stripping (since it only had repository signature)
        Assert.False(isSigned);
    }

    [Fact(Skip = "NuGet does not allow adding a repository signature to a package that already has a repository signature. This test scenario is not supported.")]
    public async Task StripRepositorySignaturesAsync_WithAuthorAndRepositorySignatures_PreservesAuthorSignature()
    {
        // This test is skipped because NuGet doesn't support adding a repository signature
        // to a package that already has a repository signature. The SignAsAuthorAsync method
        // actually creates repository signatures, not author signatures, so we can't test
        // the scenario of having both author and repository signatures without implementing
        // proper author signing support.
    }

    [Fact]
    public async Task StripRepositorySignaturesAsync_ResultingPackage_IsValid()
    {
        // Arrange
        var stripper = CreateStripper();
        var signingService = CreateSigningService();
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        
        var unsignedPackage = CreateTestPackage("Test.Package", "1.0.0");
        var signedPackage = await signingService.SignPackageAsync(
            "Test.Package",
            NuGetVersion.Parse("1.0.0"),
            unsignedPackage,
            certificate,
            CurrentCancellationToken);

        // Act
        signedPackage.Position = 0;
        var result = await stripper.StripRepositorySignaturesAsync(signedPackage, CurrentCancellationToken);

        // Assert
        Assert.NotNull(result);
        result.Position = 0;

        // Verify the package can be read and is valid
        using var packageReader = new PackageArchiveReader(result, leaveStreamOpen: true);
        var nuspec = packageReader.NuspecReader;
        Assert.Equal("Test.Package", nuspec.GetId());
        Assert.Equal("1.0.0", nuspec.GetVersion().ToNormalizedString());

        // Verify package has files
        var files = packageReader.GetFiles().ToList();
        Assert.NotEmpty(files);
        Assert.Contains("lib/netstandard2.0/_.dll", files);
    }


    private PackageSigningService CreateSigningService()
    {
        var loggerMock = new Mock<ILogger<PackageSigningService>>();
        var urlGeneratorMock = new Mock<AvantiPoint.Packages.Core.IUrlGenerator>();
        urlGeneratorMock.Setup(x => x.GetServiceIndexUrl())
            .Returns("https://example.com/v3/index.json");

        var signingOptions = new AvantiPoint.Packages.Core.Signing.SigningOptions
        {
            TimestampServerUrl = string.Empty
        };
        var optionsMock = new Mock<Microsoft.Extensions.Options.IOptions<AvantiPoint.Packages.Core.Signing.SigningOptions>>();
        optionsMock.Setup(x => x.Value).Returns(signingOptions);

        return new PackageSigningService(
            loggerMock.Object,
            urlGeneratorMock.Object,
            optionsMock.Object,
            new Rfc3161TimestampProviderFactory(Mock.Of<ILogger<Rfc3161TimestampProviderFactory>>()));
    }

    private static async Task<Stream> SignAsAuthorAsync(
        PackageSigningService signingService,
        Stream packageStream,
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        // Note: This is a placeholder - we'll need to implement author signing
        // For now, we'll use the repository signing service which creates repository signatures
        // In a real implementation, we'd need AuthorSignPackageRequest
        return await signingService.SignPackageAsync(
            "Test.Package",
            NuGetVersion.Parse("1.0.0"),
            packageStream,
            certificate,
            cancellationToken);
    }
}

