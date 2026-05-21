using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NuGet.Versioning;
namespace AvantiPoint.Packages.Integration.Tests;

internal sealed class IntegrationTestUrlGenerator : IUrlGenerator
{
    public string GetServiceIndexUrl() => IntegrationTestHostBuilder.ServiceIndexUrl;
    public string GetPackageContentResourceUrl() => "https://example.test/v3/package";
    public string GetPackageMetadataResourceUrl() => "https://example.test/v3/registration";
    public string GetPackageMetadataResourceGzipSemVer1Url() => "https://example.test/v3/registration-gz-semver1";
    public string GetPackageMetadataResourceGzipSemVer2Url() => "https://example.test/v3/registration-gz-semver2";
    public string GetPackagePublishResourceUrl() => "https://example.test/v3/package";
    public string GetSymbolPublishResourceUrl() => "https://example.test/v3/symbol";
    public string GetSearchResourceUrl() => "https://example.test/v3/search";
    public string GetAutocompleteResourceUrl() => "https://example.test/v3/autocomplete";
    public string GetVulnerabilityIndexUrl() => "https://example.test/v3/vulnerabilities/index.json";
    public string GetPackageReadmeResourceUrl() => "https://example.test/v3/package/{lower_id}/{lower_version}/readme";
    public string GetRepositorySignaturesUrl() => "https://example.test/v3/repository-signatures/index.json";
    public string GetCertificateDownloadUrl(string fingerprint) => $"https://example.test/v3/certificates/{fingerprint.ToLowerInvariant()}.crt";
    public string GetRegistrationIndexUrl(string id) => $"https://example.test/v3/registration/{id.ToLowerInvariant()}/index.json";
    public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper) => $"https://example.test/v3/registration/{id.ToLowerInvariant()}/page.json";
    public string GetRegistrationLeafUrl(string id, NuGetVersion version) => $"https://example.test/v3/registration/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}.json";
    public string GetPackageVersionsUrl(string id) => $"https://example.test/v3/package/{id.ToLowerInvariant()}/index.json";
    public string GetPackageDownloadUrl(string id, NuGetVersion version) => $"https://example.test/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.ToLowerInvariant()}.{version.ToNormalizedString().ToLowerInvariant()}.nupkg";
    public string GetPackageManifestDownloadUrl(string id, NuGetVersion version) => $"https://example.test/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.ToLowerInvariant()}.nuspec";
    public string GetPackageIconDownloadUrl(string id, NuGetVersion version) => $"https://example.test/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/icon";
    public string GetPackageLicenseDownloadUrl(string id, NuGetVersion version) => $"https://example.test/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/license";
    public string GetPackageReadmeDownloadUrl(string id, NuGetVersion version) => $"https://example.test/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/readme";
}
