using AvantiPoint.Packages.Core;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Tests;

public class RegistrationBuilderTests
{
    private class TestUrlGenerator : IUrlGenerator
    {
        public string GetServiceIndexUrl() => "https://example.com/v3/index.json";
        public string GetPackageContentResourceUrl() => "https://example.com/v3/package";
        public string GetPackageMetadataResourceUrl() => "https://example.com/v3/registration";
        public string GetPackageMetadataResourceGzipSemVer1Url() => "https://example.com/v3/registration-gz-semver1";
        public string GetPackageMetadataResourceGzipSemVer2Url() => "https://example.com/v3/registration-gz-semver2";
        public string GetPackagePublishResourceUrl() => "https://example.com/v3/package";
        public string GetSymbolPublishResourceUrl() => "https://example.com/v3/symbol";
        public string GetSearchResourceUrl() => "https://example.com/v3/search";
        public string GetAutocompleteResourceUrl() => "https://example.com/v3/autocomplete";
        public string GetVulnerabilityIndexUrl() => "https://example.com/v3/vulnerabilities/index.json";
        public string GetPackageReadmeResourceUrl() => "https://example.com/v3/package/{lower_id}/{lower_version}/readme";

        public string GetRegistrationIndexUrl(string id)
            => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/index.json";

        public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper)
            => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/page.json";

        public string GetRegistrationLeafUrl(string id, NuGetVersion version)
            => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}.json";

        public string GetPackageVersionsUrl(string id)
            => $"https://example.com/v3/package/{id.ToLowerInvariant()}/index.json";

        public string GetPackageDownloadUrl(string id, NuGetVersion version)
            => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.ToLowerInvariant()}.{version.ToNormalizedString().ToLowerInvariant()}.nupkg";

        public string GetPackageManifestDownloadUrl(string id, NuGetVersion version)
            => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.ToLowerInvariant()}.nuspec";

        public string GetPackageIconDownloadUrl(string id, NuGetVersion version)
            => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/icon";

        public string GetPackageLicenseDownloadUrl(string id, NuGetVersion version)
            => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/license";

        public string GetPackageReadmeDownloadUrl(string id, NuGetVersion version)
            => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/readme";

        public string GetRepositorySignaturesUrl()
            => "https://example.com/v3/repository-signatures/index.json";
    }

    [Fact]
    public void BuildIndex_IncludesReadmeUrl_WhenPackageHasReadme()
    {
        // Arrange
        var urlGenerator = new TestUrlGenerator();
        var packageId = "TestPackage";
        var version = NuGetVersion.Parse("1.0.0");
        var expectedReadmeUrl = $"https://example.com/v3/package/{packageId.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/readme";

        var builder = new RegistrationBuilder(urlGenerator);

        var package = new Package
        {
            Id = packageId,
            Version = version,
            Authors = new[] { "Test Author" },
            Description = "Test Description",
            HasReadme = true,
            Listed = true,
            Published = DateTime.UtcNow,
            RequireLicenseAcceptance = false,
            Summary = "Test Summary",
            Title = "Test Title",
            Tags = new[] { "test" },
            Dependencies = new List<PackageDependency>(),
            PackageTypes = new List<PackageType>(),
            TargetFrameworks = new List<TargetFramework>(),
            PackageDownloads = new List<PackageDownload>()
        };

        var registration = new PackageRegistration(packageId, new List<Package> { package });

        // Act
        var result = builder.BuildIndex(registration);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Pages);
        Assert.Single(result.Pages);

        var page = result.Pages.First();
        Assert.NotNull(page.ItemsOrNull);
        Assert.Single(page.ItemsOrNull);

        var item = page.ItemsOrNull.First();
        Assert.NotNull(item.PackageMetadata);
        Assert.NotNull(item.PackageMetadata.ReadmeUrl);
        Assert.Equal(expectedReadmeUrl, item.PackageMetadata.ReadmeUrl);
    }

    [Fact]
    public void BuildIndex_ExcludesReadmeUrl_WhenPackageDoesNotHaveReadme()
    {
        // Arrange
        var urlGenerator = new TestUrlGenerator();
        var packageId = "TestPackage";
        var version = NuGetVersion.Parse("1.0.0");

        var builder = new RegistrationBuilder(urlGenerator);

        var package = new Package
        {
            Id = packageId,
            Version = version,
            Authors = new[] { "Test Author" },
            Description = "Test Description",
            HasReadme = false, // No README
            Listed = true,
            Published = DateTime.UtcNow,
            RequireLicenseAcceptance = false,
            Summary = "Test Summary",
            Title = "Test Title",
            Tags = new[] { "test" },
            Dependencies = new List<PackageDependency>(),
            PackageTypes = new List<PackageType>(),
            TargetFrameworks = new List<TargetFramework>(),
            PackageDownloads = new List<PackageDownload>()
        };

        var registration = new PackageRegistration(packageId, new List<Package> { package });

        // Act
        var result = builder.BuildIndex(registration);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Pages);
        Assert.Single(result.Pages);

        var page = result.Pages.First();
        Assert.NotNull(page.ItemsOrNull);
        Assert.Single(page.ItemsOrNull);

        var item = page.ItemsOrNull.First();
        Assert.NotNull(item.PackageMetadata);
        Assert.Null(item.PackageMetadata.ReadmeUrl);
    }
}
