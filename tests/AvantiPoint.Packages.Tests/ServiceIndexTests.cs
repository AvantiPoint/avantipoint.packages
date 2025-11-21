using System.Net.Http.Json;
using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.Tests.Fixtures;
using Xunit.Abstractions;

namespace AvantiPoint.Packages.Tests;

public class ServiceIndexTests : IClassFixture<ServiceIndexTestFixture>, IDisposable
{
    private readonly ServiceIndexTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ServiceIndexTests(ServiceIndexTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public async Task ServiceIndex_IncludesAllRequiredEndpoints()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/v3/index.json");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var serviceIndex = await response.Content.ReadFromJsonAsync<ServiceIndexResponse>();

        Assert.NotNull(serviceIndex);
        Assert.NotNull(serviceIndex.Resources);
        Assert.NotEmpty(serviceIndex.Resources);

        _output.WriteLine($"Service Index version: {serviceIndex.Version}");
        _output.WriteLine($"Total resources: {serviceIndex.Resources.Count}");

        // Verify required resources are present
        AssertResourceExists(serviceIndex, "PackagePublish/2.0.0", "Package Publish");
        AssertResourceExists(serviceIndex, "SymbolPackagePublish/4.9.0", "Symbol Package Publish");
        AssertResourceExists(serviceIndex, "SearchQueryService/3.0.0-rc", "Search Query Service");
        AssertResourceExists(serviceIndex, "SearchQueryService/3.5.0", "Search Query Service 3.5.0");
        
        // Registration hives
        AssertResourceExists(serviceIndex, "RegistrationsBaseUrl", "Registrations Base URL (legacy)");
        AssertResourceExists(serviceIndex, "RegistrationsBaseUrl/3.0.0-rc", "Registrations Base URL 3.0.0-rc");
        AssertResourceExists(serviceIndex, "RegistrationsBaseUrl/3.4.0", "Registrations Base URL 3.4.0 (SemVer1, gzip)");
        AssertResourceExists(serviceIndex, "RegistrationsBaseUrl/3.6.0", "Registrations Base URL 3.6.0 (SemVer2, gzip)");
        AssertResourceExists(serviceIndex, "RegistrationsBaseUrl/Versioned", "Registrations Base URL Versioned");
        
        AssertResourceExists(serviceIndex, "PackageBaseAddress/3.0.0", "Package Base Address");
        AssertResourceExists(serviceIndex, "SearchAutocompleteService/3.0.0-rc", "Search Autocomplete Service");
        AssertResourceExists(serviceIndex, "SearchAutocompleteService/3.5.0", "Search Autocomplete Service 3.5.0");
        AssertResourceExists(serviceIndex, "ReadmeUriTemplate/6.13.0", "Readme URI Template");
        AssertResourceExists(serviceIndex, "VulnerabilityInfo/6.7.0", "Vulnerability Info");
        AssertResourceExists(serviceIndex, "RepositorySignatures/5.0.0", "Repository Signatures");
    }

    [Fact]
    public async Task ServiceIndex_RegistrationsBaseUrl_Versioned_HasClientVersion()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/v3/index.json");
        response.EnsureSuccessStatusCode();
        var serviceIndex = await response.Content.ReadFromJsonAsync<ServiceIndexResponse>();

        // Assert
        var versionedResource = serviceIndex?.Resources?
            .FirstOrDefault(r => r.Type == "RegistrationsBaseUrl/Versioned");

        Assert.NotNull(versionedResource);
        Assert.NotNull(versionedResource.ClientVersion);
        Assert.Equal("4.3.0-alpha", versionedResource.ClientVersion);
        _output.WriteLine($"Versioned resource client version: {versionedResource.ClientVersion}");
    }

    [Fact]
    public async Task ServiceIndex_RepositorySignatures_ResourceUrlMatchesUrlGenerator()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/v3/index.json");
        response.EnsureSuccessStatusCode();
        var serviceIndex = await response.Content.ReadFromJsonAsync<ServiceIndexResponse>();

        // Assert
        var repositorySignaturesResource = serviceIndex?.Resources?
            .FirstOrDefault(r => r.Type == "RepositorySignatures/5.0.0");

        Assert.NotNull(repositorySignaturesResource);
        Assert.NotNull(repositorySignaturesResource.ResourceUrl);
        // The URL should match the pattern for repository signatures endpoint
        Assert.Contains("/v3/repository-signatures/index.json", repositorySignaturesResource.ResourceUrl);
        _output.WriteLine($"RepositorySignatures resource URL: {repositorySignaturesResource.ResourceUrl}");
    }

    [Fact]
    public async Task ServiceIndex_RegistrationHives_HaveCorrectUrls()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/v3/index.json");
        response.EnsureSuccessStatusCode();
        var serviceIndex = await response.Content.ReadFromJsonAsync<ServiceIndexResponse>();

        // Assert
        var semVer1Resource = serviceIndex?.Resources?
            .FirstOrDefault(r => r.Type == "RegistrationsBaseUrl/3.4.0");
        var semVer2Resource = serviceIndex?.Resources?
            .FirstOrDefault(r => r.Type == "RegistrationsBaseUrl/3.6.0");
        var versionedResource = serviceIndex?.Resources?
            .FirstOrDefault(r => r.Type == "RegistrationsBaseUrl/Versioned");

        Assert.NotNull(semVer1Resource);
        Assert.Contains("registration-gz-semver1", semVer1Resource.ResourceUrl);
        _output.WriteLine($"SemVer1 gzip resource URL: {semVer1Resource.ResourceUrl}");

        Assert.NotNull(semVer2Resource);
        Assert.Contains("registration-gz-semver2", semVer2Resource.ResourceUrl);
        _output.WriteLine($"SemVer2 gzip resource URL: {semVer2Resource.ResourceUrl}");

        Assert.NotNull(versionedResource);
        Assert.Equal(semVer2Resource.ResourceUrl, versionedResource.ResourceUrl);
        _output.WriteLine($"Versioned resource points to SemVer2 URL: {versionedResource.ResourceUrl}");
    }

    [Fact]
    public async Task VulnerabilityIndex_IsAccessible()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act - First get the service index to find the vulnerability endpoint
        var indexResponse = await client.GetAsync("/v3/index.json");
        indexResponse.EnsureSuccessStatusCode();
        var serviceIndex = await indexResponse.Content.ReadFromJsonAsync<ServiceIndexResponse>();

        var vulnerabilityResource = serviceIndex?.Resources?
            .FirstOrDefault(r => r.Type?.StartsWith("VulnerabilityInfo") == true);

        Assert.NotNull(vulnerabilityResource);
        _output.WriteLine($"Vulnerability resource URL: {vulnerabilityResource.ResourceUrl}");

        // Act - Access the vulnerability index endpoint directly
        var vulnerabilityResponse = await client.GetAsync("/v3/vulnerabilities/index.json");

        // Assert
        vulnerabilityResponse.EnsureSuccessStatusCode();
        var content = await vulnerabilityResponse.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        _output.WriteLine($"Vulnerability index response: {content}");
    }

    [Fact]
    public async Task VulnerabilityIndex_ReturnsValidJson_WhenEnabled()
    {
        // Arrange - Use a client with vulnerability support enabled
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/v3/vulnerabilities/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<VulnerabilityIndexResponse>();
        
        Assert.NotNull(content);
        Assert.Equal("6.7.0", content.Version);
        Assert.NotNull(content.Pages);
        _output.WriteLine($"Vulnerability index version: {content.Version}");
        _output.WriteLine($"Number of pages: {content.Pages.Count}");
    }

    [Fact]
    public async Task VulnerabilityIndex_ReturnsEmptyPages_WhenDisabled()
    {
        // Arrange - Use a client with vulnerability support disabled
        var client = _fixture.CreateClient(vulnerabilityEnabled: false);

        // Act
        var response = await client.GetAsync("/v3/vulnerabilities/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<VulnerabilityIndexResponse>();
        
        Assert.NotNull(content);
        Assert.Equal("6.7.0", content.Version);
        Assert.NotNull(content.Pages);
        Assert.Empty(content.Pages); // Should be empty when disabled
        _output.WriteLine("Vulnerability index returned empty pages as expected when disabled");
    }

    [Fact]
    public async Task ReadmeUriTemplate_ContainsRequiredPlaceholders()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/v3/index.json");
        response.EnsureSuccessStatusCode();
        var serviceIndex = await response.Content.ReadFromJsonAsync<ServiceIndexResponse>();

        // Assert
        var readmeResource = serviceIndex?.Resources?
            .FirstOrDefault(r => r.Type == "ReadmeUriTemplate/6.13.0");

        Assert.NotNull(readmeResource);
        Assert.NotNull(readmeResource.ResourceUrl);
        
        // Verify the template contains the required placeholders
        Assert.Contains("{lower_id}", readmeResource.ResourceUrl);
        Assert.Contains("{lower_version}", readmeResource.ResourceUrl);
        
        _output.WriteLine($"Readme URI Template: {readmeResource.ResourceUrl}");
    }

    private void AssertResourceExists(ServiceIndexResponse serviceIndex, string resourceType, string resourceName)
    {
        var resource = serviceIndex.Resources.FirstOrDefault(r => r.Type == resourceType);
        Assert.NotNull(resource);
        Assert.False(string.IsNullOrWhiteSpace(resource.ResourceUrl), 
            $"{resourceName} resource URL should not be empty");
        _output.WriteLine($"✓ {resourceName} ({resourceType}): {resource.ResourceUrl}");
    }
}
