using AvantiPoint.Packages.Core;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Search.Tests.TestInfrastructure;

internal sealed class TestUrlGenerator : IUrlGenerator
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
    public string GetRegistrationIndexUrl(string id) => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/index.json";
    public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper) => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/page.json";
    public string GetRegistrationLeafUrl(string id, NuGetVersion version) => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}.json";
    public string GetPackageVersionsUrl(string id) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/index.json";
    public string GetPackageDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.ToLowerInvariant()}.{version.ToNormalizedString().ToLowerInvariant()}.nupkg";
    public string GetPackageManifestDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.ToLowerInvariant()}.nuspec";
    public string GetPackageIconDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/icon";
    public string GetPackageLicenseDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/license";
    public string GetPackageReadmeDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/readme";
    public string GetRepositorySignaturesUrl() => "https://example.com/v3/repository-signatures/index.json";
    public string GetCertificateDownloadUrl(string fingerprint) => $"https://example.com/v3/certificates/{fingerprint}.crt";
}
