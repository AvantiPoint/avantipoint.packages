using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol.Tests;

public class MetadataTests
{
    private const string FeedUrl = "https://api.nuget.org/v3/index.json";
    private const string PackageId = "Newtonsoft.Json";
    private static readonly NuGetVersion PackageVersion = new ("12.0.1");

    [Fact]
    public async Task GetAllPackageMetadata_ReturnsVersionsAndBasicFields()
    {
        var client = new NuGetClient(FeedUrl);

        var items = await client.GetPackageMetadataAsync(PackageId);

        Assert.NotNull(items);
        Assert.NotEmpty(items);

        // Ensure requested version is present
        Assert.Contains(items, m => m.Version == PackageVersion.OriginalVersion);

        // Basic field sanity
        foreach (var m in items)
        {
            Assert.NotNull(m.Version);
            // Listed can be false; just assert it's set (bool default OK)
            Assert.False(string.IsNullOrWhiteSpace(m.Description), $"Description should be populated for {m.Version}");
        }
    }

    [Fact]
    public async Task GetPackageMetadata_ReturnsExpectedVersion()
    {
        var client = new NuGetClient(FeedUrl);

        var metadata = await client.GetPackageMetadataAsync(PackageId, PackageVersion);

        Assert.NotNull(metadata);
        Assert.Equal(PackageVersion.OriginalVersion, metadata.Version);
        Assert.False(string.IsNullOrWhiteSpace(metadata.Description), "Description should be populated.");
    }

    [Fact]
    public async Task ListVersions_IncludesRequestedVersion()
    {
        var client = new NuGetClient(FeedUrl);

        var packageVersions = await client.ListPackageVersionsAsync(PackageId, includeUnlisted: true);

        Assert.NotNull(packageVersions);
        Assert.NotEmpty(packageVersions);
        Assert.Contains(packageVersions, v => v == PackageVersion);

        // Version ordering is not guaranteed; just ensure uniqueness.
        Assert.Equal(packageVersions.Count, packageVersions.Distinct().Count());
    }
}