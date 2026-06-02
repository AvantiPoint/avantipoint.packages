using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
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
    public async Task Search_ClampsNegativePaginationParameters()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var packageName = $"negative-page-{Guid.NewGuid():N}";
        var publishBody = BuildNpmPublishBody(packageName, "1.0.0", tarballBytes: [0x1f, 0x8b, 0x08]);

        var publishResponse = await client.PutAsync(
            $"/npm/{packageName}",
            new StringContent(publishBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, publishResponse.StatusCode);

        var response = await client.GetAsync($"/npm/-/v1/search?text={packageName}&from=-10&size=-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var search = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(1, search["total"]!.GetValue<int>());
        Assert.Empty(search["objects"]!.AsArray());
    }

    [Fact]
    public async Task MirroringScopedPackage_DoesNotMarkSameTarballNameFromAnotherPackageAsMirrored()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var shortName = $"collision-{Guid.NewGuid():N}";
        var scopedName = $"@scope/{shortName}";
        var encodedScopedName = scopedName.Replace("/", "%2f", StringComparison.Ordinal);
        var version = "1.0.0";
        FakeNpmMirrorService.PackageName = scopedName;
        FakeNpmMirrorService.Version = version;

        var publishBody = BuildNpmPublishBody(shortName, version, tarballBytes: [0x1f, 0x8b, 0x08]);
        var publishResponse = await client.PutAsync(
            $"/npm/{shortName}",
            new StringContent(publishBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, publishResponse.StatusCode);

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INpmMirrorService>();
                services.AddScoped<INpmMirrorService, FakeNpmMirrorService>();
            });
        });

        var mirrorClient = factory.CreateClient();
        mirrorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var mirrorResponse = await mirrorClient.GetAsync($"/npm/{encodedScopedName}");
        Assert.Equal(HttpStatusCode.OK, mirrorResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var published = context.NpmVersions.Single(v => v.Package.Name == shortName && v.Version == version);
        var mirrored = context.NpmVersions.Single(v => v.Package.Name == scopedName && v.Version == version);

        Assert.Equal(PackageOrigin.Published, published.Origin);
        Assert.Equal(PackageOrigin.Mirrored, mirrored.Origin);
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

    private sealed class FakeNpmMirrorService : INpmMirrorService
    {
        public static string PackageName { get; set; } = string.Empty;

        public static string Version { get; set; } = string.Empty;

        public MirrorCachingStrategy Strategy => MirrorCachingStrategy.IndexAndCache;

        public PackageOrigin MirrorOrigin => PackageOrigin.Mirrored;

        public Task<JsonObject?> FetchPackumentAsync(string packageName, CancellationToken cancellationToken = default)
        {
            var tarballFileName = GetTarballFileName(PackageName, Version);
            JsonObject packument = new()
            {
                ["name"] = PackageName,
                ["dist-tags"] = new JsonObject
                {
                    ["latest"] = Version,
                },
                ["versions"] = new JsonObject
                {
                    [Version] = new JsonObject
                    {
                        ["name"] = PackageName,
                        ["version"] = Version,
                        ["dist"] = new JsonObject
                        {
                            ["tarball"] = $"https://example.test/{tarballFileName}",
                        },
                    },
                },
            };

            return Task.FromResult<JsonObject?>(packument);
        }

        public Task<Stream?> FetchTarballAsync(string tarballUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(new MemoryStream([0x1f, 0x8b, 0x08]));

        private static string GetTarballFileName(string packageName, string version)
        {
            var shortName = packageName.Contains('@')
                ? packageName[(packageName.IndexOf('/') + 1)..]
                : packageName;

            return $"{shortName}-{version}.tgz";
        }
    }
}
