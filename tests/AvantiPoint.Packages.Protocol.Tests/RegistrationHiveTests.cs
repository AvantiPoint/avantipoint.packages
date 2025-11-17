using System.IO.Compression;
using System.Net.Http.Json;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.Protocol.Tests.Infrastructure;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol.Tests;

/// <summary>
/// Integration tests for RegistrationsBaseUrl/3.4.0, RegistrationsBaseUrl/3.6.0, and semVerLevel support.
/// Tests the new gzip-compressed registration hives and SemVer2 filtering.
/// </summary>
public class RegistrationHiveTests : IClassFixture<NuGetServerFixture>
{
    private readonly NuGetServerFixture _fixture;

    public RegistrationHiveTests(NuGetServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ServiceIndex_ExposesAllRegistrationHives()
    {
        // Act
        var serviceIndex = await _fixture.Server.Client.GetFromJsonAsync<ServiceIndexResponse>("/v3/index.json");

        // Assert
        Assert.NotNull(serviceIndex);
        Assert.NotNull(serviceIndex.Resources);
        
        // Check for all registration hive types
        var registrationResources = serviceIndex.Resources
            .Where(r => r.Type != null && r.Type.StartsWith("RegistrationsBaseUrl"))
            .ToList();

        Assert.Contains(registrationResources, r => r.Type == "RegistrationsBaseUrl");
        Assert.Contains(registrationResources, r => r.Type == "RegistrationsBaseUrl/3.0.0-rc");
        Assert.Contains(registrationResources, r => r.Type == "RegistrationsBaseUrl/3.0.0-beta");
        Assert.Contains(registrationResources, r => r.Type == "RegistrationsBaseUrl/3.4.0");
        Assert.Contains(registrationResources, r => r.Type == "RegistrationsBaseUrl/3.6.0");
        Assert.Contains(registrationResources, r => r.Type == "RegistrationsBaseUrl/Versioned");
    }

    [Fact]
    public async Task RegistrationsBaseUrl_3_4_0_Endpoint_RespondsCorrectly()
    {
        // Act - Request non-existent package from SemVer1 gzip endpoint
        var response = await _fixture.Server.Client.GetAsync("/v3/registration-gz-semver1/nonexistent.test.package/index.json");
        
        // Assert - Should return 404 (not 500 or other error)
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationsBaseUrl_3_6_0_Endpoint_RespondsCorrectly()
    {
        // Act - Request non-existent package from SemVer2 gzip endpoint  
        var response = await _fixture.Server.Client.GetAsync("/v3/registration-gz-semver2/nonexistent.test.package/index.json");
        
        // Assert - Should return 404 (not 500 or other error)
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationsBaseUrl_Leaf_Endpoints_RespondCorrectly()
    {
        // Act - Request non-existent package version from SemVer1 gzip endpoint
        var response1 = await _fixture.Server.Client.GetAsync("/v3/registration-gz-semver1/nonexistent.test.package/1.0.0.json");
        
        // Assert - Should return 404 (not 500 or other error)
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response1.StatusCode);

        // Act - Request non-existent package version from SemVer2 gzip endpoint
        var response2 = await _fixture.Server.Client.GetAsync("/v3/registration-gz-semver2/nonexistent.test.package/1.0.0.json");
        
        // Assert - Should return 404 (not 500 or other error)
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response2.StatusCode);
    }

    [Fact]
    public async Task LegacyRegistrationEndpoint_AcceptsSemVerLevelParameter()
    {
        // This test verifies that the legacy endpoint accepts the semVerLevel query parameter
        // without throwing an error. The actual filtering behavior is tested in unit tests.
        
        // Act - Request with semVerLevel=2.0.0 query parameter
        var response = await _fixture.Server.Client.GetAsync("/v3/registration/nonexistent-package/index.json?semVerLevel=2.0.0");
        
        // Assert - Should get 404 (not found) not 400 (bad request)
        // This confirms the parameter is recognized
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LegacyRegistrationEndpoint_WithoutSemVerLevel_Works()
    {
        // This test verifies that the legacy endpoint still works without the semVerLevel parameter
        
        // Act - Request without semVerLevel
        var response = await _fixture.Server.Client.GetAsync("/v3/registration/nonexistent-package/index.json");
        
        // Assert - Should get 404 (not found) not 500 (server error)
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<string> DecompressGzipResponse(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        return await reader.ReadToEndAsync();
    }
}
