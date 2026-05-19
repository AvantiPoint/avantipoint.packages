using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AvantiPoint.Packages.Registry.Npm.Tests;

public class NpmRegistryTests : IClassFixture<NpmTestWebApplicationFactory>
{
    private const string ApiKey = "integration-test-key";
    private readonly NpmTestWebApplicationFactory _factory;

    public NpmRegistryTests(NpmTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task EnsureDatabaseAsync() => await _factory.EnsureDatabaseMigratedAsync();

    [Fact]
    public async Task WhoAmI_ReturnsOk_WithBearerToken()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/npm/-/whoami");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublishAndGetPackument_RoundTrip()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var packageName = $"test-pkg-{Guid.NewGuid():N}";
        var version = "1.0.0";

        var publishBody = BuildNpmPublishBody(packageName, version, tarballBytes: [0x1f, 0x8b, 0x08]);
        var publishResponse = await client.PutAsync(
            $"/npm/{packageName}",
            new StringContent(publishBody, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, publishResponse.StatusCode);

        var packumentResponse = await client.GetAsync($"/npm/{packageName}");
        Assert.Equal(HttpStatusCode.OK, packumentResponse.StatusCode);

        var json = await packumentResponse.Content.ReadAsStringAsync();
        var packument = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(packageName, packument["name"]!.GetValue<string>());
        Assert.True(packument["versions"]![version] is JsonObject);
    }

    [Fact]
    public async Task UnregisteredOciPath_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v2/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        return client;
    }

    private static string BuildNpmPublishBody(string name, string version, byte[] tarballBytes)
    {
        var root = new JsonObject
        {
            ["name"] = name,
            ["versions"] = new JsonObject
            {
                [version] = new JsonObject
                {
                    ["name"] = name,
                    ["version"] = version,
                    ["dist"] = new JsonObject
                    {
                        ["shasum"] = "abc",
                        ["tarball"] = $"{name}-{version}.tgz",
                    },
                },
            },
            ["attachments"] = new JsonObject
            {
                [$"{name}-{version}.tgz"] = new JsonObject
                {
                    ["content_type"] = "application/octet-stream",
                    ["data"] = Convert.ToBase64String(tarballBytes),
                },
            },
        };

        return root.ToJsonString();
    }
}
