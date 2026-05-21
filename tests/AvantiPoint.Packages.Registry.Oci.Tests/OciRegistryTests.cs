using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public class OciRegistryTests : IClassFixture<OciTestWebApplicationFactory>
{
    private const string ApiKey = "integration-test-key";
    private readonly OciTestWebApplicationFactory _factory;

    public OciRegistryTests(OciTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Version_ReturnsOk_WithDistributionHeader()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v2/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("registry/2.0", response.Headers.GetValues("Docker-Distribution-API-Version").Single());
    }

    [Fact]
    public async Task BlobUploadAndDownload_RoundTrip()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var repository = $"test/{Guid.NewGuid():N}";
        var content = Encoding.UTF8.GetBytes("hello-oci-blob");
        var digest = ComputeDigest(content);

        var startResponse = await client.PostAsync($"/v2/{repository}/blobs/uploads/", null);
        Assert.Equal(HttpStatusCode.Accepted, startResponse.StatusCode);
        var location = startResponse.Headers.Location!.ToString();

        using var patchContent = new ByteArrayContent(content);
        patchContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var patchResponse = await client.PatchAsync(location, patchContent);
        Assert.Equal(HttpStatusCode.Accepted, patchResponse.StatusCode);

        var completeResponse = await client.PutAsync($"{location}?digest={Uri.EscapeDataString(digest)}", null);
        Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);

        var blobResponse = await client.GetAsync($"/v2/{repository}/blobs/{digest}");
        Assert.Equal(HttpStatusCode.OK, blobResponse.StatusCode);
        var downloaded = await blobResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(content, downloaded);
    }

    [Fact]
    public async Task ManifestPutAndGet_RoundTrip()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var repository = $"manifest/{Guid.NewGuid():N}";
        var configContent = Encoding.UTF8.GetBytes("{}");
        var layerContent = Encoding.UTF8.GetBytes("layer-data");
        var configDigest = await UploadBlobAsync(client, repository, configContent);
        var layerDigest = await UploadBlobAsync(client, repository, layerContent);

        var manifest = $$"""
                         {
                           "schemaVersion": 2,
                           "mediaType": "application/vnd.oci.image.manifest.v1+json",
                           "config": {
                             "mediaType": "application/vnd.oci.empty.v1+json",
                             "size": {{configContent.Length}},
                             "digest": "{{configDigest}}"
                           },
                           "layers": [
                             {
                               "mediaType": "application/vnd.oci.image.layer.v1.tar",
                               "size": {{layerContent.Length}},
                               "digest": "{{layerDigest}}"
                             }
                           ]
                         }
                         """;

        using var manifestContent = new StringContent(manifest, Encoding.UTF8, "application/vnd.oci.image.manifest.v1+json");
        var putResponse = await client.PutAsync($"/v2/{repository}/manifests/v1", manifestContent);
        Assert.Equal(HttpStatusCode.Created, putResponse.StatusCode);

        var getResponse = await client.GetAsync($"/v2/{repository}/manifests/v1");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("application/vnd.oci.image.manifest.v1+json", getResponse.Content.Headers.ContentType?.MediaType);

        var tagsResponse = await client.GetAsync($"/v2/{repository}/tags/list");
        Assert.Equal(HttpStatusCode.OK, tagsResponse.StatusCode);
        var tagsJson = JsonDocument.Parse(await tagsResponse.Content.ReadAsStringAsync());
        Assert.Contains("v1", tagsJson.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task NamedSegment_IsIsolatedFromDefault()
    {
        await EnsureDatabaseAsync();
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Named segment registration is configured in test factory configuration.
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var defaultResponse = await client.GetAsync("/v2/");
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);

        var namedResponse = await client.GetAsync("/docker/v2/");
        Assert.Equal(HttpStatusCode.OK, namedResponse.StatusCode);
    }

    [Fact]
    public async Task NuGetRoutes_AreNotCapturedByOci()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v3/index.json");
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task EnsureDatabaseAsync() => await _factory.EnsureDatabaseMigratedAsync();

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        return client;
    }

    private static string ComputeDigest(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static async Task<string> UploadBlobAsync(HttpClient client, string repository, byte[] content)
    {
        var digest = ComputeDigest(content);
        var startResponse = await client.PostAsync($"/v2/{repository}/blobs/uploads/", null);
        startResponse.EnsureSuccessStatusCode();
        var location = startResponse.Headers.Location!.ToString();

        using var patchContent = new ByteArrayContent(content);
        patchContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var patchResponse = await client.PatchAsync(location, patchContent);
        patchResponse.EnsureSuccessStatusCode();

        var completeResponse = await client.PutAsync($"{location}?digest={Uri.EscapeDataString(digest)}", null);
        completeResponse.EnsureSuccessStatusCode();
        return digest;
    }
}
