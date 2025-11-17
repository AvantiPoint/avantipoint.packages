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
        AssertResourceExists(serviceIndex, "RegistrationsBaseUrl/3.0.0-rc", "Registrations Base URL");
        AssertResourceExists(serviceIndex, "PackageBaseAddress/3.0.0", "Package Base Address");
        AssertResourceExists(serviceIndex, "SearchAutocompleteService/3.0.0-rc", "Search Autocomplete Service");
        AssertResourceExists(serviceIndex, "VulnerabilityInfo/6.7.0", "Vulnerability Info");
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
        var client = _fixture.CreateClientWithVulnerabilityDisabled();

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

    private void AssertResourceExists(ServiceIndexResponse serviceIndex, string resourceType, string resourceName)
    {
        var resource = serviceIndex.Resources.FirstOrDefault(r => r.Type == resourceType);
        Assert.NotNull(resource);
        Assert.False(string.IsNullOrWhiteSpace(resource.ResourceUrl), 
            $"{resourceName} resource URL should not be empty");
        _output.WriteLine($"✓ {resourceName} ({resourceType}): {resource.ResourceUrl}");
    }
}
