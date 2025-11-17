using AvantiPoint.Packages.Protocol.Tests.Infrastructure;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol.Tests;

/// <summary>
/// Integration tests demonstrating the in-process test server.
/// These tests exercise the NuGet protocol against a live, isolated server instance.
/// </summary>
public class InProcessServerTests : IClassFixture<NuGetServerFixture>
{
    private readonly NuGetServerFixture _fixture;

    public InProcessServerTests(NuGetServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Server_IsRunningAndAccessible()
    {
        // Arrange & Act
        var response = await _fixture.Server.Client.GetAsync("/v3/index.json");

        // Assert
        Assert.True(response.IsSuccessStatusCode, "Server should respond to service index requests");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"version\":", content);
    }

    [Fact]
    public async Task PushPackage_ThenRetrieveMetadata_Succeeds()
    {
        // Arrange
        var packageId = "Test.Integration.Package";
        var version = "1.0.0";
        
        // Act - Upload package
        var uploadResponse = await TestPackageHelper.CreateAndUploadPackageAsync(
            _fixture.Server.Client,
            packageId,
            version);

        // Assert upload succeeded
        var uploadContent = await uploadResponse.Content.ReadAsStringAsync();
        Assert.True(uploadResponse.IsSuccessStatusCode, 
            $"Package upload should succeed. Status: {uploadResponse.StatusCode}, Content: {uploadContent}");

        // Small delay to allow indexing to complete
        await Task.Delay(200);

        // Act - Retrieve metadata using NuGetClient
        var client = _fixture.Client;
        var metadata = await client.GetPackageMetadataAsync(packageId, NuGetVersion.Parse(version));

        // Assert metadata is correct
        Assert.NotNull(metadata);
        Assert.Equal(version, metadata.Version);
        Assert.Equal(packageId, metadata.PackageId);
        Assert.Contains("Test package", metadata.Description);
    }

    [Fact]
    public async Task PushPackage_ThenSearch_FindsPackage()
    {
        // Arrange
        var packageId = "Test.Search.Package";
        var version = "2.0.0";
        
        await TestPackageHelper.CreateAndUploadPackageAsync(
            _fixture.Server.Client,
            packageId,
            version);

        // Wait a moment for indexing
        await Task.Delay(100);

        // Act
        var client = _fixture.Client;
        var results = await client.SearchAsync(packageId);

        // Assert
        Assert.NotNull(results);
        Assert.Contains(results, r => r.PackageId == packageId);
    }

    [Fact]
    public async Task ListVersions_AfterUploadingMultipleVersions_ReturnsAllVersions()
    {
        // Arrange
        var packageId = "Test.MultiVersion.Package";
        
        await TestPackageHelper.CreateAndUploadPackageAsync(_fixture.Server.Client, packageId, "1.0.0");
        await TestPackageHelper.CreateAndUploadPackageAsync(_fixture.Server.Client, packageId, "1.1.0");
        await TestPackageHelper.CreateAndUploadPackageAsync(_fixture.Server.Client, packageId, "2.0.0");

        // Act
        var client = _fixture.Client;
        var versions = await client.ListPackageVersionsAsync(packageId, includeUnlisted: true);

        // Assert
        Assert.NotNull(versions);
        Assert.Equal(3, versions.Count);
        Assert.Contains(NuGetVersion.Parse("1.0.0"), versions);
        Assert.Contains(NuGetVersion.Parse("1.1.0"), versions);
        Assert.Contains(NuGetVersion.Parse("2.0.0"), versions);
    }

    [Fact]
    public async Task PackageExists_ReturnsTrueForUploadedPackage()
    {
        // Arrange
        var packageId = "Test.Exists.Package";
        var version = "1.5.0";
        
        await TestPackageHelper.CreateAndUploadPackageAsync(_fixture.Server.Client, packageId, version);

        // Act
        var client = _fixture.Client;
        var existsId = await client.ExistsAsync(packageId);
        var existsVersion = await client.ExistsAsync(packageId, NuGetVersion.Parse(version));

        // Assert
        Assert.True(existsId, $"Package {packageId} should exist");
        Assert.True(existsVersion, $"Package {packageId} version {version} should exist");
    }

    [Fact]
    public async Task PackageExists_ReturnsFalseForNonExistentPackage()
    {
        // Arrange
        var packageId = "NonExistent.Package.That.Does.Not.Exist";

        // Act
        var client = _fixture.Client;
        var exists = await client.ExistsAsync(packageId);

        // Assert
        Assert.False(exists, "Non-existent package should return false");
    }

    [Fact]
    public async Task DownloadPackage_ReturnsPackageContent()
    {
        // Arrange
        var packageId = "Test.Download.Package";
        var version = "3.0.0";
        
        var uploadedBytes = TestPackageHelper.CreatePackage(packageId, version);
        await TestPackageHelper.UploadPackageAsync(_fixture.Server.Client, uploadedBytes);

        // Act
        var client = _fixture.Client;
        var downloadedBytes = await client.DownloadPackageAsync(packageId, NuGetVersion.Parse(version));

        // Assert
        Assert.NotNull(downloadedBytes);
        Assert.True(downloadedBytes.Length > 0, "Downloaded package should have content");
    }
}
