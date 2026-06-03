using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task ParallelBlobUploadStarts_OnNewRepository_DoNotReturnUnauthorized()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var repository = $"parallel/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => client.PostAsync($"/v2/{repository}/blobs/uploads/", null, cancellationToken));
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, response => Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task BlobUploadWithFinalChunkInPutBody_RoundTrip()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var repository = $"test/{Guid.NewGuid():N}";
        var firstChunk = Encoding.UTF8.GetBytes("hello-");
        var finalChunk = Encoding.UTF8.GetBytes("oci-blob");
        var content = firstChunk.Concat(finalChunk).ToArray();
        var digest = ComputeDigest(content);

        var startResponse = await client.PostAsync($"/v2/{repository}/blobs/uploads/", null);
        Assert.Equal(HttpStatusCode.Accepted, startResponse.StatusCode);
        var location = startResponse.Headers.Location!.ToString();

        using var patchContent = new ByteArrayContent(firstChunk);
        patchContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var patchResponse = await client.PatchAsync(location, patchContent);
        Assert.Equal(HttpStatusCode.Accepted, patchResponse.StatusCode);

        using var putContent = new ByteArrayContent(finalChunk);
        putContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var completeResponse = await client.PutAsync($"{location}?digest={Uri.EscapeDataString(digest)}", putContent);
        Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);

        var blobResponse = await client.GetAsync($"/v2/{repository}/blobs/{digest}");
        Assert.Equal(HttpStatusCode.OK, blobResponse.StatusCode);
        Assert.Equal(content.Length, blobResponse.Content.Headers.ContentLength);
        var downloaded = await blobResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(content, downloaded);
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
        Assert.Contains($"/blobs/{digest}", completeResponse.Headers.Location!.ToString());
        Assert.DoesNotContain("/blobs/uploads/", completeResponse.Headers.Location!.ToString());

        var blobResponse = await client.GetAsync($"/v2/{repository}/blobs/{digest}");
        Assert.Equal(HttpStatusCode.OK, blobResponse.StatusCode);
        Assert.Equal(content.Length, blobResponse.Content.Headers.ContentLength);
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

        var embeddedHelmResponse = await client.GetAsync("/v2/helm/");
        Assert.Equal(HttpStatusCode.OK, embeddedHelmResponse.StatusCode);
    }

    [Fact]
    public async Task ManifestPut_InvalidJson_ReturnsBadRequest()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var repository = $"manifest/{Guid.NewGuid():N}";

        using var manifestContent = new StringContent("{not-json", Encoding.UTF8, "application/vnd.oci.image.manifest.v1+json");
        var putResponse = await client.PutAsync($"/v2/{repository}/manifests/v1", manifestContent);
        Assert.Equal(HttpStatusCode.BadRequest, putResponse.StatusCode);

        var body = JsonDocument.Parse(await putResponse.Content.ReadAsStringAsync());
        Assert.Equal("MANIFEST_INVALID", body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ManifestGet_ByDigest_IsScopedToRepository()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var repositoryA = $"repo-a/{Guid.NewGuid():N}";
        var repositoryB = $"repo-b/{Guid.NewGuid():N}";
        var configContent = Encoding.UTF8.GetBytes("{}");
        var layerContent = Encoding.UTF8.GetBytes("layer-data");
        var configDigest = await UploadBlobAsync(client, repositoryA, configContent);
        var layerDigest = await UploadBlobAsync(client, repositoryA, layerContent);

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
        var putResponse = await client.PutAsync($"/v2/{repositoryA}/manifests/v1", manifestContent);
        putResponse.EnsureSuccessStatusCode();
        var manifestDigest = putResponse.Headers.GetValues("Docker-Content-Digest").Single();

        var crossRepoResponse = await client.GetAsync($"/v2/{repositoryB}/manifests/{manifestDigest}");
        Assert.Equal(HttpStatusCode.NotFound, crossRepoResponse.StatusCode);

        var sameRepoResponse = await client.GetAsync($"/v2/{repositoryA}/manifests/{manifestDigest}");
        Assert.Equal(HttpStatusCode.OK, sameRepoResponse.StatusCode);
    }

    [Fact]
    public async Task MirroredManifestGet_PersistsManifestWithoutLocalReferencedBlobs()
    {
        var referencedConfigDigest = ComputeDigest(Encoding.UTF8.GetBytes("upstream-config"));
        var referencedLayerDigest = ComputeDigest(Encoding.UTF8.GetBytes("upstream-layer"));
        var manifest = $$"""
                         {
                           "schemaVersion": 2,
                           "mediaType": "application/vnd.oci.image.manifest.v1+json",
                           "config": {
                             "mediaType": "application/vnd.oci.empty.v1+json",
                             "size": 15,
                             "digest": "{{referencedConfigDigest}}"
                           },
                           "layers": [
                             {
                               "mediaType": "application/vnd.oci.image.layer.v1.tar",
                               "size": 14,
                               "digest": "{{referencedLayerDigest}}"
                             }
                           ]
                         }
                         """;
        var manifestBytes = Encoding.UTF8.GetBytes(manifest);
        var upstream = new OciUpstreamManifest(
            ComputeDigest(manifestBytes),
            "application/vnd.oci.image.manifest.v1+json",
            manifestBytes);

        var mirror = new TestOciMirrorService(upstream);
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOciMirrorService>();
                services.AddSingleton<IOciMirrorService>(mirror);
            });
        });

        await EnsureDatabaseAsync(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var repository = $"mirror/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await client.GetAsync($"/v2/{repository}/manifests/v1", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(upstream.Digest, response.Headers.GetValues("Docker-Content-Digest").Single());

        var secondResponse = await client.GetAsync($"/v2/{repository}/manifests/v1", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(1, mirror.FetchManifestCalls);
    }

    [Fact]
    public async Task MirroredBlobGet_FallsBackToUpstreamWhenLocalBlobRecordHasNoFile()
    {
        var blobContent = Encoding.UTF8.GetBytes("upstream-layer");
        var blobDigest = ComputeDigest(blobContent);
        var manifest = $$"""
                         {
                           "schemaVersion": 2,
                           "mediaType": "application/vnd.oci.image.manifest.v1+json",
                           "config": {
                             "mediaType": "application/vnd.oci.empty.v1+json",
                             "size": {{blobContent.Length}},
                             "digest": "{{blobDigest}}"
                           },
                           "layers": []
                         }
                         """;
        var manifestBytes = Encoding.UTF8.GetBytes(manifest);
        var upstream = new OciUpstreamManifest(
            ComputeDigest(manifestBytes),
            "application/vnd.oci.image.manifest.v1+json",
            manifestBytes);

        var mirror = new TestOciMirrorService(upstream, blobDigest, blobContent);
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOciMirrorService>();
                services.AddSingleton<IOciMirrorService>(mirror);
            });
        });

        await EnsureDatabaseAsync(factory);
        var repository = $"mirror/{Guid.NewGuid():N}";
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var manifestResponse = await client.GetAsync($"/v2/{repository}/manifests/v1");
        Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            context.OciBlobs.Add(new OciBlob
            {
                Digest = blobDigest,
                Size = blobContent.Length,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var blobResponse = await client.GetAsync($"/v2/{repository}/blobs/{blobDigest}");

        Assert.Equal(HttpStatusCode.OK, blobResponse.StatusCode);
        Assert.Equal(blobContent, await blobResponse.Content.ReadAsByteArrayAsync());
        Assert.Equal(1, mirror.FetchBlobCalls);
    }

    [Fact]
    public async Task MirroredBlobGet_RejectsUpstreamContentWithMismatchedDigest()
    {
        var validBlobContent = Encoding.UTF8.GetBytes($"upstream-layer-{Guid.NewGuid():N}");
        var blobDigest = ComputeDigest(validBlobContent);
        var corruptBlobContent = Encoding.UTF8.GetBytes($"corrupt-upstream-layer-{Guid.NewGuid():N}");
        var manifest = $$"""
                         {
                           "schemaVersion": 2,
                           "mediaType": "application/vnd.oci.image.manifest.v1+json",
                           "config": {
                             "mediaType": "application/vnd.oci.empty.v1+json",
                             "size": {{validBlobContent.Length}},
                             "digest": "{{blobDigest}}"
                           },
                           "layers": []
                         }
                         """;
        var manifestBytes = Encoding.UTF8.GetBytes(manifest);
        var upstream = new OciUpstreamManifest(
            ComputeDigest(manifestBytes),
            "application/vnd.oci.image.manifest.v1+json",
            manifestBytes);

        var mirror = new TestOciMirrorService(upstream, blobDigest, corruptBlobContent);
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOciMirrorService>();
                services.AddSingleton<IOciMirrorService>(mirror);
            });
        });

        await EnsureDatabaseAsync(factory);
        var repository = $"mirror/{Guid.NewGuid():N}";
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var cancellationToken = TestContext.Current.CancellationToken;

        var manifestResponse = await client.GetAsync($"/v2/{repository}/manifests/v1", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, manifestResponse.StatusCode);

        var blobResponse = await client.GetAsync($"/v2/{repository}/blobs/{blobDigest}", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, blobResponse.StatusCode);
        Assert.Equal(1, mirror.FetchBlobCalls);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        Assert.False(context.OciBlobs.Any(b => b.Digest == blobDigest));
    }

    [Fact]
    public async Task NuGetRoutes_AreNotCapturedByOci()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v3/index.json");
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task EnsureDatabaseAsync() => await _factory.EnsureDatabaseMigratedAsync();

    private static async Task EnsureDatabaseAsync(WebApplicationFactory<IntegrationTestApi.Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        await context.RunMigrationsAsync(TestContext.Current.CancellationToken);
    }

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

    private sealed class TestOciMirrorService(
        OciUpstreamManifest manifest,
        string? blobDigest = null,
        byte[]? blobContent = null) : IOciMirrorService
    {
        public int FetchManifestCalls { get; private set; }

        public int FetchBlobCalls { get; private set; }

        public Task<OciUpstreamManifest?> TryFetchManifestAsync(
            SurfaceContext surface,
            string repositoryName,
            string reference,
            CancellationToken cancellationToken = default)
        {
            FetchManifestCalls++;
            return Task.FromResult<OciUpstreamManifest?>(manifest);
        }

        public Task<Stream?> TryFetchBlobAsync(
            SurfaceContext surface,
            string repositoryName,
            string digest,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(digest, blobDigest, StringComparison.OrdinalIgnoreCase) || blobContent is null)
            {
                return Task.FromResult<Stream?>(null);
            }

            FetchBlobCalls++;
            return Task.FromResult<Stream?>(new MemoryStream(blobContent));
        }

        public Task<OciBlobExistsResult?> TryCheckBlobExistsAsync(
            SurfaceContext surface,
            string repositoryName,
            string digest,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OciBlobExistsResult?>(null);

        public PackageOrigin MirrorOrigin(SurfaceContext surface) => PackageOrigin.Mirrored;

        public MirrorCachingStrategy Strategy(SurfaceContext surface) => MirrorCachingStrategy.IndexAndCache;

        public bool ShouldPersist(SurfaceContext surface) => true;
    }
}
