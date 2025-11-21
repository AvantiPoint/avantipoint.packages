using System;
using System.IO;
using System.Linq;
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
        var result = await stripper.StripRepositorySignaturesAsync(packageStream);

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
        
        // Create package with author signature only
        var unsignedPackage = CreateTestPackage("Test.Package", "1.0.0");
        var authorSignedPackage = await SignAsAuthorAsync(signingService, unsignedPackage, certificate);

        // Act
        var result = await stripper.StripRepositorySignaturesAsync(authorSignedPackage);

        // Assert
        Assert.NotNull(result);
        result.Position = 0;

        // Verify package still has author signature
        using var packageReader = new PackageArchiveReader(result, leaveStreamOpen: true);
        var isSigned = await packageReader.IsSignedAsync(default);
        Assert.True(isSigned);

        var primarySignature = await packageReader.GetPrimarySignatureAsync(default);
        Assert.NotNull(primarySignature);
        Assert.Equal(SignatureType.Author, primarySignature.Type);
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
            certificate);

        // Verify it has repository signature
        repositorySignedPackage.Position = 0;
        using var beforeReader = new PackageArchiveReader(repositorySignedPackage, leaveStreamOpen: true);
        var beforeSignature = await beforeReader.GetPrimarySignatureAsync(default);
        Assert.NotNull(beforeSignature);
        Assert.Equal(SignatureType.Repository, beforeSignature.Type);

        // Act
        repositorySignedPackage.Position = 0;
        var result = await stripper.StripRepositorySignaturesAsync(repositorySignedPackage);

        // Assert
        Assert.NotNull(result);
        result.Position = 0;

        // Note: Stripping a repository signature from a package that only has a repository signature
        // will result in an unsigned package. This is expected behavior.
        using var afterReader = new PackageArchiveReader(result, leaveStreamOpen: true);
        var isSigned = await afterReader.IsSignedAsync(default);
        
        // Package should be unsigned after stripping (since it only had repository signature)
        Assert.False(isSigned);
    }

    [Fact]
    public async Task StripRepositorySignaturesAsync_WithAuthorAndRepositorySignatures_PreservesAuthorSignature()
    {
        // Arrange
        var stripper = CreateStripper();
        var signingService = CreateSigningService();
        var authorCert = TestCertificateHelper.CreateTestCertificate("CN=Author Certificate");
        var repoCert = TestCertificateHelper.CreateTestCertificate("CN=Repository Certificate");
        
        // Create package with author signature first
        var unsignedPackage = CreateTestPackage("Test.Package", "1.0.0");
        var authorSignedPackage = await SignAsAuthorAsync(signingService, unsignedPackage, authorCert);

        // Add repository signature as countersignature
        authorSignedPackage.Position = 0;
        var dualSignedPackage = await signingService.SignPackageAsync(
            "Test.Package",
            NuGetVersion.Parse("1.0.0"),
            authorSignedPackage,
            repoCert);

        // Verify it has both signatures
        dualSignedPackage.Position = 0;
        using var beforeReader = new PackageArchiveReader(dualSignedPackage, leaveStreamOpen: true);
        var beforeSignature = await beforeReader.GetPrimarySignatureAsync(default);
        Assert.NotNull(beforeSignature);
        Assert.Equal(SignatureType.Author, beforeSignature.Type);
        // Note: Repository signature would be a countersignature, which is harder to verify directly

        // Act
        dualSignedPackage.Position = 0;
        var result = await stripper.StripRepositorySignaturesAsync(dualSignedPackage);

        // Assert
        Assert.NotNull(result);
        result.Position = 0;

        // Verify package still has author signature
        using var afterReader = new PackageArchiveReader(result, leaveStreamOpen: true);
        var isSigned = await afterReader.IsSignedAsync(default);
        Assert.True(isSigned, "Package should still be signed after stripping repository signature");

        var afterSignature = await afterReader.GetPrimarySignatureAsync(default);
        Assert.NotNull(afterSignature);
        Assert.Equal(SignatureType.Author, afterSignature.Type);
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
            certificate);

        // Act
        signedPackage.Position = 0;
        var result = await stripper.StripRepositorySignaturesAsync(signedPackage);

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

    [Fact]
    public async Task StripRepositorySignaturesAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var stripper = CreateStripper();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            stripper.StripRepositorySignaturesAsync(null!));
    }

    private PackageSigningService CreateSigningService()
    {
        var loggerMock = new Mock<ILogger<PackageSigningService>>();
        var urlGeneratorMock = new Mock<AvantiPoint.Packages.Core.IUrlGenerator>();
        urlGeneratorMock.Setup(x => x.GetServiceIndexUrl())
            .Returns("https://example.com/v3/index.json");

        var signingOptions = new AvantiPoint.Packages.Core.Signing.SigningOptions
        {
            TimestampServerUrl = "http://timestamp.digicert.com"
        };
        var optionsMock = new Mock<Microsoft.Extensions.Options.IOptions<AvantiPoint.Packages.Core.Signing.SigningOptions>>();
        optionsMock.Setup(x => x.Value).Returns(signingOptions);

        return new PackageSigningService(
            loggerMock.Object,
            urlGeneratorMock.Object,
            optionsMock.Object);
    }

    private static async Task<Stream> SignAsAuthorAsync(
        PackageSigningService signingService,
        Stream packageStream,
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
    {
        // Note: This is a placeholder - we'll need to implement author signing
        // For now, we'll use the repository signing service which creates repository signatures
        // In a real implementation, we'd need AuthorSignPackageRequest
        return await signingService.SignPackageAsync(
            "Test.Package",
            NuGetVersion.Parse("1.0.0"),
            packageStream,
            certificate);
    }
}

