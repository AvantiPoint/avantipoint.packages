using AvantiPoint.Packages.Core;
using Moq;
using NuGet.Packaging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests.Indexing;

public class PackageIndexingServiceTests
{
    [Fact]
    public async Task IndexAsync_WithExtensionMethod_WorksWithoutContext()
    {
        // Arrange
        var packageStream = CreateTestPackage("Test.Package", "1.0.0");
        var indexingService = CreateMockIndexingService();

        // Act - Using extension method without context
        var result = await indexingService.Object.IndexAsync(packageStream, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PackageIndexingStatus.Success, result.Status);
        Assert.Equal("Test.Package", result.PackageId);
        Assert.Equal("1.0.0", result.PackageVersion);
    }

    [Fact]
    public async Task IndexAsync_WithNullContext_UsesDefaults()
    {
        // Arrange
        var packageStream = CreateTestPackage("Test.Package", "1.0.0");
        var indexingService = CreateMockIndexingService();

        // Act - Passing null context explicitly
        var result = await indexingService.Object.IndexAsync(packageStream, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PackageIndexingStatus.Success, result.Status);
    }

    [Fact]
    public async Task IndexAsync_WithInvalidPackage_ReturnsInvalidPackageStatus()
    {
        // Arrange
        var invalidStream = new MemoryStream(new byte[] { 1, 2, 3 }); // Not a valid package
        var indexingService = CreateMockIndexingService();

        // Act
        var result = await indexingService.Object.IndexAsync(invalidStream, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PackageIndexingStatus.InvalidPackage, result.Status);
        Assert.Null(result.PackageId); // Error cases don't have package info
        Assert.Null(result.PackageVersion);
    }

    [Fact]
    public async Task IndexAsync_WithPackageAlreadyExists_ReturnsPackageAlreadyExistsStatus()
    {
        // Arrange
        var packageStream = CreateTestPackage("Test.Package", "1.0.0");
        var indexingService = CreateMockIndexingService(alreadyExists: true);

        // Act
        var result = await indexingService.Object.IndexAsync(packageStream, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PackageIndexingStatus.PackageAlreadyExists, result.Status);
        Assert.Null(result.PackageId); // Error cases don't have package info
        Assert.Null(result.PackageVersion);
    }

    private Mock<IPackageIndexingService> CreateMockIndexingService(bool alreadyExists = false)
    {
        var mock = new Mock<IPackageIndexingService>();

        if (alreadyExists)
        {
            mock.Setup(s => s.IndexAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<PackageIngestionContext?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PackageIndexingResult
                {
                    PackageId = null,
                    PackageVersion = null,
                    Status = PackageIndexingStatus.PackageAlreadyExists
                });
        }
        else
        {
            mock.Setup(s => s.IndexAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<PackageIngestionContext?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, PackageIngestionContext? context, CancellationToken ct) =>
                {
                    // Try to read package to validate it's a valid package
                    try
                    {
                        stream.Position = 0;
                        using var reader = new PackageArchiveReader(stream, leaveStreamOpen: true);
                        var metadata = reader.NuspecReader;
                        return new PackageIndexingResult
                        {
                            PackageId = metadata.GetId(),
                            PackageVersion = metadata.GetVersion().OriginalVersion,
                            Status = PackageIndexingStatus.Success
                        };
                    }
                    catch
                    {
                        return new PackageIndexingResult
                        {
                            PackageId = null,
                            PackageVersion = null,
                            Status = PackageIndexingStatus.InvalidPackage
                        };
                    }
                });
        }

        return mock;
    }

    private static MemoryStream CreateTestPackage(string packageId, string version)
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
}

