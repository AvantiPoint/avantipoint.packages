using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform.Callbacks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task PublishAndDownloadTarball_ScopedPackage_RoundTrip()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var packageName = $"@scope/test-{Guid.NewGuid():N}";
        var version = "1.0.0";
        var encodedName = packageName.Replace("/", "%2f", StringComparison.Ordinal);

        var publishBody = BuildNpmPublishBody(packageName, version, tarballBytes: [0x1f, 0x8b, 0x08], useUnderscoreAttachments: true);
        var publishResponse = await client.PutAsync(
            $"/npm/{encodedName}",
            new StringContent(publishBody, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, publishResponse.StatusCode);

        var packumentResponse = await client.GetAsync($"/npm/{encodedName}");
        Assert.Equal(HttpStatusCode.OK, packumentResponse.StatusCode);

        var packument = JsonNode.Parse(await packumentResponse.Content.ReadAsStringAsync())!.AsObject();
        var tarballUrl = packument["versions"]![version]!["dist"]!["tarball"]!.GetValue<string>();
        Assert.StartsWith("http", tarballUrl, StringComparison.OrdinalIgnoreCase);

        var tarballFileName = $"{packageName[(packageName.IndexOf('/') + 1)..]}-{version}.tgz";
        var tarballResponse = await client.GetAsync($"/npm/{encodedName}/-/{tarballFileName}");
        Assert.Equal(HttpStatusCode.OK, tarballResponse.StatusCode);
    }

    [Fact]
    public async Task GetTarball_WhenHandlerDeniesAccess_ReturnsForbidden()
    {
        await EnsureDatabaseAsync();
        var packageName = $"deny-pkg-{Guid.NewGuid():N}";
        var version = "1.0.0";

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFeedActionHandler>();
                services.AddSingleton<IFeedActionHandler, DenyNpmArtifactHandler>();
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var publishBody = BuildNpmPublishBody(packageName, version, tarballBytes: [0x1f, 0x8b, 0x08]);
        var publishResponse = await client.PutAsync(
            $"/npm/{packageName}",
            new StringContent(publishBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, publishResponse.StatusCode);

        var tarballFileName = $"{packageName}-{version}.tgz";
        var tarballResponse = await client.GetAsync($"/npm/{packageName}/-/{tarballFileName}");
        Assert.Equal(HttpStatusCode.Forbidden, tarballResponse.StatusCode);
    }

    [Fact]
    public async Task Publish_WhenBodyExceedsLimit_ReturnsPayloadTooLarge()
    {
        await EnsureDatabaseAsync();
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Feed:Npm:MaxPublishBodyBytes"] = "32",
                });
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var publishBody = BuildNpmPublishBody($"oversize-{Guid.NewGuid():N}", "1.0.0", tarballBytes: [0x1f, 0x8b, 0x08]);
        var response = await client.PutAsync(
            $"/npm/oversize-{Guid.NewGuid():N}",
            new StringContent(publishBody, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task PublishAndGetPackument_UsesForwardedPublicBaseUrl()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "packages.example.com");
        client.DefaultRequestHeaders.Add("X-Forwarded-Prefix", "/myfeed");

        var packageName = $"fwd-pkg-{Guid.NewGuid():N}";
        var version = "1.0.0";
        var publishBody = BuildNpmPublishBody(packageName, version, tarballBytes: [0x1f, 0x8b, 0x08]);
        var publishResponse = await client.PutAsync(
            $"/npm/{packageName}",
            new StringContent(publishBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, publishResponse.StatusCode);

        var packument = JsonNode.Parse(await (await client.GetAsync($"/npm/{packageName}")).Content.ReadAsStringAsync())!.AsObject();
        var tarballUrl = packument["versions"]![version]!["dist"]!["tarball"]!.GetValue<string>();
        Assert.StartsWith("https://packages.example.com/myfeed/npm/", tarballUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnregisteredOciSegment_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/unregistered-segment/v2/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        return client;
    }

    private static string BuildNpmPublishBody(
        string name,
        string version,
        byte[] tarballBytes,
        bool useUnderscoreAttachments = false)
    {
        var shortName = name.Contains('@') ? name[(name.IndexOf('/') + 1)..] : name;
        var tarballKey = $"{shortName}-{version}.tgz";
        var attachment = new JsonObject
        {
            ["content_type"] = "application/octet-stream",
            ["data"] = Convert.ToBase64String(tarballBytes),
        };

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
                        ["tarball"] = tarballKey,
                    },
                },
            },
        };

        if (useUnderscoreAttachments)
        {
            root["_attachments"] = new JsonObject { [tarballKey] = attachment };
        }
        else
        {
            root["attachments"] = new JsonObject { [tarballKey] = attachment };
        }

        return root.ToJsonString();
    }
}
