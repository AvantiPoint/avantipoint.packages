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
using Microsoft.EntityFrameworkCore;
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
        var response = await client.GetAsync("/v2/", TestContext.Current.CancellationToken);
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

        var startResponse = await client.PostAsync(
            $"/v2/{repository}/blobs/uploads/",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, startResponse.StatusCode);
        var location = startResponse.Headers.Location!.ToString();

        using var patchContent = new ByteArrayContent(firstChunk);
        patchContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var patchResponse = await client.PatchAsync(
            location,
            patchContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, patchResponse.StatusCode);

        using var putContent = new ByteArrayContent(finalChunk);
        putContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var completeResponse = await client.PutAsync(
            $"{location}?digest={Uri.EscapeDataString(digest)}",
            putContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);

        var blobResponse = await client.GetAsync(
            $"/v2/{repository}/blobs/{digest}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, blobResponse.StatusCode);
        Assert.Equal(content.Length, blobResponse.Content.Headers.ContentLength);
        var downloaded = await blobResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
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

        var startResponse = await client.PostAsync(
            $"/v2/{repository}/blobs/uploads/",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, startResponse.StatusCode);
        var location = startResponse.Headers.Location!.ToString();

        using var patchContent = new ByteArrayContent(content);
        patchContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var patchResponse = await client.PatchAsync(
            location,
            patchContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, patchResponse.StatusCode);

        var completeResponse = await client.PutAsync(
            $"{location}?digest={Uri.EscapeDataString(digest)}",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);
        Assert.Contains($"/blobs/{digest}", completeResponse.Headers.Location!.ToString());
        Assert.DoesNotContain("/blobs/uploads/", completeResponse.Headers.Location!.ToString());

        var blobResponse = await client.GetAsync(
            $"/v2/{repository}/blobs/{digest}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, blobResponse.StatusCode);
        Assert.Equal(content.Length, blobResponse.Content.Headers.ContentLength);
        var downloaded = await blobResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
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
        var putResponse = await client.PutAsync(
            $"/v2/{repository}/manifests/v1",
            manifestContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, putResponse.StatusCode);
        Assert.DoesNotContain("//v2/", putResponse.Headers.Location!.AbsolutePath, StringComparison.Ordinal);
        var manifestDigest = putResponse.Headers.GetValues("Docker-Content-Digest").Single();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            var storedManifest = await context.OciManifests
                .AsNoTracking()
                .SingleAsync(
                    item => item.Digest == manifestDigest,
                    TestContext.Current.CancellationToken);
            Assert.Equal(OciArtifactKind.Image, storedManifest.ArtifactKind);
        }
        Assert.Contains(
            _factory.ArtifactHandler.Uploads,
            upload => upload.ArtifactName == repository
                      && upload.Version == "v1"
                      && upload.DigestOrTarballPath == manifestDigest);

        var uploadCount = _factory.ArtifactHandler.Uploads.Count(upload => upload.ArtifactName == repository);
        using var digestManifestContent = new StringContent(
            manifest,
            Encoding.UTF8,
            "application/vnd.oci.image.manifest.v1+json");
        var digestPutResponse = await client.PutAsync(
            $"/v2/{repository}/manifests/{manifestDigest}",
            digestManifestContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, digestPutResponse.StatusCode);
        Assert.Equal(
            uploadCount,
            _factory.ArtifactHandler.Uploads.Count(upload => upload.ArtifactName == repository));

        var getResponse = await client.GetAsync(
            $"/v2/{repository}/manifests/v1",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("application/vnd.oci.image.manifest.v1+json", getResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            _factory.ArtifactHandler.Downloads,
            download => download.ArtifactName == repository
                        && download.Version == "v1"
                        && download.DigestOrTarballPath == manifestDigest);

        var tagsResponse = await client.GetAsync(
            $"/v2/{repository}/tags/list",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, tagsResponse.StatusCode);
        var tagsJson = JsonDocument.Parse(
            await tagsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("v1", tagsJson.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task ReferrersApi_IndexesSubjectArtifactsAndFiltersByArtifactType()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var repository = $"referrers/{Guid.NewGuid():N}";
        var emptyContent = Encoding.UTF8.GetBytes("{}");
        var layerContent = Encoding.UTF8.GetBytes("image-layer");
        var emptyDigest = await UploadBlobAsync(client, repository, emptyContent);
        var layerDigest = await UploadBlobAsync(client, repository, layerContent);
        var imageManifest = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 2,
              "mediaType": "application/vnd.oci.image.manifest.v1+json",
              "config": {
                "mediaType": "application/vnd.oci.image.config.v1+json",
                "size": {{emptyContent.Length}},
                "digest": "{{emptyDigest}}"
              },
              "layers": [{
                "mediaType": "application/vnd.oci.image.layer.v1.tar",
                "size": {{layerContent.Length}},
                "digest": "{{layerDigest}}"
              }]
            }
            """);
        using (var imageContent = new ByteArrayContent(imageManifest))
        {
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.oci.image.manifest.v1+json");
            var imageResponse = await client.PutAsync(
                $"/v2/{repository}/manifests/1.0.0",
                imageContent,
                TestContext.Current.CancellationToken);
            imageResponse.EnsureSuccessStatusCode();
        }
        var imageDigest = ComputeDigest(imageManifest);

        var sbomContent = Encoding.UTF8.GetBytes("{\"spdxVersion\":\"SPDX-2.3\"}");
        var sbomDigest = await UploadBlobAsync(client, repository, sbomContent);
        var artifactManifest = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 2,
              "mediaType": "application/vnd.oci.image.manifest.v1+json",
              "artifactType": "application/spdx+json",
              "config": {
                "mediaType": "application/vnd.oci.empty.v1+json",
                "size": {{emptyContent.Length}},
                "digest": "{{emptyDigest}}"
              },
              "layers": [{
                "mediaType": "application/spdx+json",
                "size": {{sbomContent.Length}},
                "digest": "{{sbomDigest}}"
              }],
              "subject": {
                "mediaType": "application/vnd.oci.image.manifest.v1+json",
                "size": {{imageManifest.Length}},
                "digest": "{{imageDigest}}"
              },
              "annotations": {
                "org.opencontainers.image.created": "2026-09-08T12:00:00Z"
              }
            }
            """);
        var artifactDigest = ComputeDigest(artifactManifest);
        using (var artifactContent = new ByteArrayContent(artifactManifest))
        {
            artifactContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.oci.image.manifest.v1+json");
            var artifactResponse = await client.PutAsync(
                $"/v2/{repository}/manifests/{artifactDigest}",
                artifactContent,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, artifactResponse.StatusCode);
            Assert.Equal(imageDigest, artifactResponse.Headers.GetValues("OCI-Subject").Single());
        }

        var referrersResponse = await client.GetAsync(
            $"/v2/{repository}/referrers/{imageDigest}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, referrersResponse.StatusCode);
        Assert.Equal(OciReferrersIndex.MediaType, referrersResponse.Content.Headers.ContentType?.MediaType);
        using var referrersJson = JsonDocument.Parse(
            await referrersResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, referrersJson.RootElement.GetProperty("schemaVersion").GetInt32());
        var descriptor = Assert.Single(referrersJson.RootElement.GetProperty("manifests").EnumerateArray());
        Assert.Equal(artifactDigest, descriptor.GetProperty("digest").GetString());
        Assert.Equal("application/spdx+json", descriptor.GetProperty("artifactType").GetString());
        Assert.Equal(
            "2026-09-08T12:00:00Z",
            descriptor.GetProperty("annotations").GetProperty("org.opencontainers.image.created").GetString());

        var filteredResponse = await client.GetAsync(
            $"/v2/{repository}/referrers/{imageDigest}?artifactType=application/vnd.example.signature",
            TestContext.Current.CancellationToken);
        Assert.Equal("artifactType", filteredResponse.Headers.GetValues("OCI-Filters-Applied").Single());
        using var filteredJson = JsonDocument.Parse(
            await filteredResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(filteredJson.RootElement.GetProperty("manifests").EnumerateArray());

        var artifactResponseByDigest = await client.GetAsync(
            $"/v2/{repository}/manifests/{artifactDigest}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, artifactResponseByDigest.StatusCode);

        var tagsResponse = await client.GetAsync(
            $"/v2/{repository}/tags/list",
            TestContext.Current.CancellationToken);
        using var tagsJson = JsonDocument.Parse(
            await tagsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ["1.0.0"],
            tagsJson.RootElement.GetProperty("tags").EnumerateArray().Select(static item => item.GetString()));
    }

    [Fact]
    public async Task ReferrersApi_InvalidDigest_ReturnsBadRequest()
    {
        var response = await CreateAuthenticatedClient().GetAsync(
            $"/v2/repository/referrers/not-a-digest",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

        var defaultResponse = await client.GetAsync("/v2/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);

        var namedResponse = await client.GetAsync("/docker/v2/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, namedResponse.StatusCode);

        var embeddedHelmResponse = await client.GetAsync(
            "/v2/helm/",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, embeddedHelmResponse.StatusCode);
    }

    [Fact]
    public async Task ManifestPut_InvalidJson_ReturnsBadRequest()
    {
        await EnsureDatabaseAsync();
        var client = CreateAuthenticatedClient();
        var repository = $"manifest/{Guid.NewGuid():N}";

        using var manifestContent = new StringContent("{not-json", Encoding.UTF8, "application/vnd.oci.image.manifest.v1+json");
        var putResponse = await client.PutAsync(
            $"/v2/{repository}/manifests/v1",
            manifestContent,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, putResponse.StatusCode);

        var body = JsonDocument.Parse(
            await putResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
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
        var putResponse = await client.PutAsync(
            $"/v2/{repositoryA}/manifests/v1",
            manifestContent,
            TestContext.Current.CancellationToken);
        putResponse.EnsureSuccessStatusCode();
        var manifestDigest = putResponse.Headers.GetValues("Docker-Content-Digest").Single();

        var crossRepoResponse = await client.GetAsync(
            $"/v2/{repositoryB}/manifests/{manifestDigest}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, crossRepoResponse.StatusCode);

        var sameRepoResponse = await client.GetAsync(
            $"/v2/{repositoryA}/manifests/{manifestDigest}",
            TestContext.Current.CancellationToken);
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
    public async Task MirroredMutableTag_WhenRefreshIsDue_UpdatesToNewDigestWithoutFetchingBlobs()
    {
        var firstManifest = CreateImageManifest("first");
        var secondManifest = CreateImageManifest("second");
        var mirror = new MutableOciMirrorService(
            new Dictionary<string, OciUpstreamManifest>(StringComparer.Ordinal)
            {
                ["latest"] = firstManifest,
            });
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Feed:Oci:Default:Mirror:MetadataRefreshInterval"] = "00:00:00",
                    ["Feed:Oci:Default:Mirror:RefreshTrackedArtifacts"] = "true",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOciMirrorService>();
                services.AddSingleton<IOciMirrorService>(mirror);
            });
        });

        await EnsureDatabaseAsync(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var repository = $"tracked/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await client.GetAsync($"/v2/{repository}/manifests/latest", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(firstManifest.Digest, first.Headers.GetValues("Docker-Content-Digest").Single());

        mirror.Manifests["latest"] = secondManifest;
        var refreshed = await client.GetAsync($"/v2/{repository}/manifests/latest", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(secondManifest.Digest, refreshed.Headers.GetValues("Docker-Content-Digest").Single());
        Assert.Equal(2, mirror.ManifestRequests.Count);
        Assert.Equal(0, mirror.FetchBlobCalls);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var storedTag = await context.OciTags
            .AsNoTracking()
            .SingleAsync(
                tag => tag.OciSegment == null
                       && tag.Repository.Name == repository
                       && tag.Tag == "latest",
                cancellationToken);
        Assert.Equal(secondManifest.Digest, storedTag.ManifestDigest);
    }

    [Fact]
    public async Task CachedMutableTag_WhenRefreshIsDue_UpdatesDigestWithoutBecomingDiscoverable()
    {
        var firstManifest = CreateImageManifest("cached-first");
        var secondManifest = CreateImageManifest("cached-second");
        var mirror = new MutableOciMirrorService(
            new Dictionary<string, OciUpstreamManifest>(StringComparer.Ordinal)
            {
                ["stable"] = firstManifest,
            })
        {
            CachingStrategy = MirrorCachingStrategy.CacheOnly,
        };
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Feed:Oci:Default:IncludeMirroredInCatalog"] = "true",
                    ["Feed:Oci:Default:Mirror:MetadataRefreshInterval"] = "00:00:00",
                    ["Feed:Oci:Default:Mirror:RefreshTrackedArtifacts"] = "true",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOciMirrorService>();
                services.AddSingleton<IOciMirrorService>(mirror);
            });
        });

        await EnsureDatabaseAsync(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var repository = $"cache/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await client.GetAsync($"/v2/{repository}/manifests/stable", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        mirror.Manifests["stable"] = secondManifest;

        var refreshed = await client.GetAsync($"/v2/{repository}/manifests/stable", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(secondManifest.Digest, refreshed.Headers.GetValues("Docker-Content-Digest").Single());

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var storedTag = await context.OciTags
            .AsNoTracking()
            .SingleAsync(tag => tag.Repository.Name == repository && tag.Tag == "stable", cancellationToken);
        Assert.Equal(secondManifest.Digest, storedTag.ManifestDigest);
        Assert.Equal(PackageOrigin.Cached, storedTag.Origin);

        var catalog = await client.GetAsync("/v2/_catalog", cancellationToken);
        catalog.EnsureSuccessStatusCode();
        using var catalogJson = JsonDocument.Parse(await catalog.Content.ReadAsStringAsync(cancellationToken));
        Assert.DoesNotContain(
            repository,
            catalogJson.RootElement.GetProperty("repositories").EnumerateArray().Select(static item => item.GetString()));
    }

    [Fact]
    public async Task TrackedHelmRepository_DiscoversOnlyNewerSemver_AndLeavesChartBlobsLazy()
    {
        var configDigest = ComputeDigest(Encoding.UTF8.GetBytes("helm-config"));
        var chartDigest = ComputeDigest(Encoding.UTF8.GetBytes("helm-chart"));
        var firstManifest = CreateHelmManifest("1.0.0", configDigest, chartDigest);
        var secondManifest = CreateHelmManifest("1.1.0", configDigest, chartDigest);
        var mirror = new MutableOciMirrorService(
            new Dictionary<string, OciUpstreamManifest>(StringComparer.Ordinal)
            {
                ["1.0.0"] = firstManifest,
                ["1.1.0"] = secondManifest,
            })
        {
            Tags = ["0.9.0", "1.0.0", "1.1.0", "latest"],
        };
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Feed:Oci:Helm:Profile"] = "HelmCharts",
                    ["Feed:Oci:Helm:IncludeMirroredInCatalog"] = "true",
                    ["Feed:Oci:Helm:Mirror:MetadataRefreshInterval"] = "00:00:00",
                    ["Feed:Oci:Helm:Mirror:TrackedTagDiscoveryLimit"] = "32",
                    ["Feed:Oci:Helm:Mirror:NewHelmVersionsPerRefresh"] = "4",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOciMirrorService>();
                services.AddSingleton<IOciMirrorService>(mirror);
            });
        });

        await EnsureDatabaseAsync(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var repository = $"charts/{Guid.NewGuid():N}";
        var unrelatedRepository = $"unrelated/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        var consumed = await client.GetAsync(
            $"/helm/v2/{repository}/manifests/1.0.0",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, consumed.StatusCode);

        var tagsResponse = await client.GetAsync(
            $"/helm/v2/{repository}/tags/list",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, tagsResponse.StatusCode);
        using var tagsJson = JsonDocument.Parse(await tagsResponse.Content.ReadAsStringAsync(cancellationToken));
        var tags = tagsJson.RootElement.GetProperty("tags")
            .EnumerateArray()
            .Select(static tag => tag.GetString())
            .ToArray();
        Assert.Contains("1.0.0", tags);
        Assert.Contains("1.1.0", tags);
        Assert.DoesNotContain("0.9.0", tags);
        Assert.DoesNotContain("latest", tags);
        Assert.Equal([(repository, 32)], mirror.TagListRequests);
        Assert.DoesNotContain(mirror.TagListRequests, request => request.Repository == unrelatedRepository);
        Assert.Contains(mirror.ManifestRequests, request => request.Repository == repository && request.Reference == "1.1.0");
        Assert.Equal(0, mirror.FetchBlobCalls);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var storedRepositories = await context.OciRepositories
            .AsNoTracking()
            .Where(item => item.OciSegment == "helm")
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);
        Assert.Contains(repository, storedRepositories);
        Assert.DoesNotContain(unrelatedRepository, storedRepositories);
        Assert.False(await context.OciBlobs.AnyAsync(
            blob => blob.OciSegment == "helm"
                    && (blob.Digest == configDigest || blob.Digest == chartDigest),
            cancellationToken));
    }

    [Fact]
    public async Task TrackedHelmRepository_ProxyOnlyRefresh_ReturnsTagsWithoutPersistingThem()
    {
        var firstManifest = CreateHelmManifest(
            "1.0.0",
            ComputeDigest(Encoding.UTF8.GetBytes("proxy-config")),
            ComputeDigest(Encoding.UTF8.GetBytes("proxy-chart")));
        var mirror = new MutableOciMirrorService(
            new Dictionary<string, OciUpstreamManifest>(StringComparer.Ordinal)
            {
                ["1.0.0"] = firstManifest,
            })
        {
            Tags = ["1.0.0", "1.1.0"],
        };
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Feed:Oci:Helm:Profile"] = "HelmCharts",
                    ["Feed:Oci:Helm:IncludeMirroredInCatalog"] = "true",
                    ["Feed:Oci:Helm:Mirror:MetadataRefreshInterval"] = "00:00:00",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOciMirrorService>();
                services.AddSingleton<IOciMirrorService>(mirror);
            });
        });

        await EnsureDatabaseAsync(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var repository = $"charts/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;
        var consumed = await client.GetAsync(
            $"/helm/v2/{repository}/manifests/1.0.0",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, consumed.StatusCode);

        mirror.CachingStrategy = MirrorCachingStrategy.ProxyOnly;
        var tagsResponse = await client.GetAsync(
            $"/helm/v2/{repository}/tags/list",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, tagsResponse.StatusCode);
        using var tagsJson = JsonDocument.Parse(await tagsResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Contains(
            "1.1.0",
            tagsJson.RootElement.GetProperty("tags").EnumerateArray().Select(static tag => tag.GetString()));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        Assert.False(await context.OciTags.AnyAsync(
            tag => tag.OciSegment == "helm"
                   && tag.Repository.Name == repository
                   && tag.Tag == "1.1.0",
            cancellationToken));
    }

    [Fact]
    public async Task TrackedHelmRepository_CacheOnlyRefresh_DoesNotIndexAdvertisedVersion()
    {
        var firstManifest = CreateHelmManifest(
            "1.0.0",
            ComputeDigest(Encoding.UTF8.GetBytes("cache-config")),
            ComputeDigest(Encoding.UTF8.GetBytes("cache-chart")));
        var mirror = new MutableOciMirrorService(
            new Dictionary<string, OciUpstreamManifest>(StringComparer.Ordinal)
            {
                ["1.0.0"] = firstManifest,
            })
        {
            Tags = ["1.0.0", "1.1.0"],
        };
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Feed:Oci:Helm:Profile"] = "HelmCharts",
                    ["Feed:Oci:Helm:IncludeMirroredInCatalog"] = "true",
                    ["Feed:Oci:Helm:Mirror:MetadataRefreshInterval"] = "00:00:00",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOciMirrorService>();
                services.AddSingleton<IOciMirrorService>(mirror);
            });
        });

        await EnsureDatabaseAsync(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var repository = $"charts/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;
        var consumed = await client.GetAsync(
            $"/helm/v2/{repository}/manifests/1.0.0",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, consumed.StatusCode);

        mirror.CachingStrategy = MirrorCachingStrategy.CacheOnly;
        var tagsResponse = await client.GetAsync(
            $"/helm/v2/{repository}/tags/list",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, tagsResponse.StatusCode);
        using var tagsJson = JsonDocument.Parse(await tagsResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.DoesNotContain(
            "1.1.0",
            tagsJson.RootElement.GetProperty("tags").EnumerateArray().Select(static tag => tag.GetString()));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        Assert.False(await context.OciTags.AnyAsync(
            tag => tag.OciSegment == "helm"
                   && tag.Repository.Name == repository
                   && tag.Tag == "1.1.0",
            cancellationToken));
        Assert.DoesNotContain(mirror.ManifestRequests, request => request.Reference == "1.1.0");
    }

    [Fact]
    public async Task MirroredManifestGet_ProxyOnlySourceDoesNotPersist()
    {
        var manifestBytes = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"layers\":[]}");
        var upstream = new OciUpstreamManifest(
            ComputeDigest(manifestBytes),
            "application/vnd.oci.image.manifest.v1+json",
            manifestBytes)
        {
            CachingStrategy = MirrorCachingStrategy.ProxyOnly,
        };

        var mirror = new TestOciMirrorService(upstream, cachingStrategy: MirrorCachingStrategy.ProxyOnly);
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
        var repository = $"proxy/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await client.GetAsync($"/v2/{repository}/manifests/latest", cancellationToken);
        var second = await client.GetAsync($"/v2/{repository}/manifests/latest", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, mirror.FetchManifestCalls);
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

        var manifestResponse = await client.GetAsync(
            $"/v2/{repository}/manifests/v1",
            TestContext.Current.CancellationToken);
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

        var blobResponse = await client.GetAsync(
            $"/v2/{repository}/blobs/{blobDigest}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, blobResponse.StatusCode);
        Assert.Equal(
            blobContent,
            await blobResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, mirror.FetchBlobCalls);
    }

    [Fact]
    public async Task MirroredBlobGet_ProxyOnlySourceDoesNotPersist()
    {
        var blobContent = Encoding.UTF8.GetBytes($"proxy-layer-{Guid.NewGuid():N}");
        var blobDigest = ComputeDigest(blobContent);
        var manifestBytes = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"layers\":[]}");
        var upstream = new OciUpstreamManifest(
            ComputeDigest(manifestBytes),
            "application/vnd.oci.image.manifest.v1+json",
            manifestBytes)
        {
            CachingStrategy = MirrorCachingStrategy.ProxyOnly,
        };

        var mirror = new TestOciMirrorService(
            upstream,
            blobDigest,
            blobContent,
            MirrorCachingStrategy.ProxyOnly);
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
        var repository = $"proxy/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await client.GetAsync($"/v2/{repository}/blobs/{blobDigest}", cancellationToken);
        var second = await client.GetAsync($"/v2/{repository}/blobs/{blobDigest}", cancellationToken);

        Assert.Equal(blobContent, await first.Content.ReadAsByteArrayAsync(cancellationToken));
        Assert.Equal(blobContent, await second.Content.ReadAsByteArrayAsync(cancellationToken));
        Assert.Equal(2, mirror.FetchBlobCalls);
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
        var response = await client.GetAsync(
            "/v3/index.json",
            TestContext.Current.CancellationToken);
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

    private static OciUpstreamManifest CreateImageManifest(string marker)
    {
        var bytes = Encoding.UTF8.GetBytes(
            $$"""
              {
                "schemaVersion": 2,
                "mediaType": "application/vnd.oci.image.manifest.v1+json",
                "annotations": { "org.opencontainers.image.revision": "{{marker}}" },
                "layers": []
              }
              """);
        return new OciUpstreamManifest(
            ComputeDigest(bytes),
            "application/vnd.oci.image.manifest.v1+json",
            bytes)
        {
            CachingStrategy = MirrorCachingStrategy.IndexAndCache,
        };
    }

    private static OciUpstreamManifest CreateHelmManifest(
        string version,
        string configDigest,
        string chartDigest)
    {
        var bytes = Encoding.UTF8.GetBytes(
            $$"""
              {
                "schemaVersion": 2,
                "mediaType": "application/vnd.oci.image.manifest.v1+json",
                "config": {
                  "mediaType": "application/vnd.cncf.helm.config.v1+json",
                  "size": 11,
                  "digest": "{{configDigest}}"
                },
                "layers": [
                  {
                    "mediaType": "application/vnd.cncf.helm.chart.content.v1.tar+gzip",
                    "size": 10,
                    "digest": "{{chartDigest}}"
                  }
                ],
                "annotations": { "org.opencontainers.image.version": "{{version}}" }
              }
              """);
        return new OciUpstreamManifest(
            ComputeDigest(bytes),
            "application/vnd.oci.image.manifest.v1+json",
            bytes)
        {
            CachingStrategy = MirrorCachingStrategy.IndexAndCache,
        };
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
        byte[]? blobContent = null,
        MirrorCachingStrategy cachingStrategy = MirrorCachingStrategy.IndexAndCache) : IOciMirrorService
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

        public async Task<OciUpstreamBlob?> TryFetchBlobWithPolicyAsync(
            SurfaceContext surface,
            string repositoryName,
            string digest,
            CancellationToken cancellationToken = default)
        {
            var content = await TryFetchBlobAsync(surface, repositoryName, digest, cancellationToken);
            return content is null ? null : new OciUpstreamBlob(content, cachingStrategy);
        }

        public Task<OciBlobExistsResult?> TryCheckBlobExistsAsync(
            SurfaceContext surface,
            string repositoryName,
            string digest,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OciBlobExistsResult?>(null);

        public PackageOrigin MirrorOrigin(SurfaceContext surface) => PackageOrigin.Mirrored;

        public MirrorCachingStrategy Strategy(SurfaceContext surface) => cachingStrategy;

        public bool ShouldPersist(SurfaceContext surface) => cachingStrategy != MirrorCachingStrategy.ProxyOnly;
    }

    private sealed class MutableOciMirrorService(
        Dictionary<string, OciUpstreamManifest> manifests) : IOciMirrorService
    {
        public Dictionary<string, OciUpstreamManifest> Manifests { get; } = manifests;

        public IReadOnlyList<string> Tags { get; init; } = [];

        public MirrorCachingStrategy CachingStrategy { get; set; } = MirrorCachingStrategy.IndexAndCache;

        public List<(string Repository, string Reference)> ManifestRequests { get; } = [];

        public List<(string Repository, int MaximumTags)> TagListRequests { get; } = [];

        public int FetchBlobCalls { get; private set; }

        public Task<OciUpstreamManifest?> TryFetchManifestAsync(
            SurfaceContext surface,
            string repositoryName,
            string reference,
            CancellationToken cancellationToken = default)
        {
            ManifestRequests.Add((repositoryName, reference));
            if (!Manifests.TryGetValue(reference, out var manifest))
            {
                return Task.FromResult<OciUpstreamManifest?>(null);
            }

            return Task.FromResult<OciUpstreamManifest?>(manifest with
            {
                CachingStrategy = CachingStrategy,
            });
        }

        public Task<OciUpstreamTagList?> TryListTagsAsync(
            SurfaceContext surface,
            string repositoryName,
            int maximumTags,
            CancellationToken cancellationToken = default)
        {
            TagListRequests.Add((repositoryName, maximumTags));
            return Task.FromResult<OciUpstreamTagList?>(
                new OciUpstreamTagList(Tags.Take(maximumTags).ToArray(), CachingStrategy));
        }

        public Task<Stream?> TryFetchBlobAsync(
            SurfaceContext surface,
            string repositoryName,
            string digest,
            CancellationToken cancellationToken = default)
        {
            FetchBlobCalls++;
            return Task.FromResult<Stream?>(null);
        }

        public Task<OciBlobExistsResult?> TryCheckBlobExistsAsync(
            SurfaceContext surface,
            string repositoryName,
            string digest,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OciBlobExistsResult?>(null);

        public PackageOrigin MirrorOrigin(SurfaceContext surface) =>
            CachingStrategy == MirrorCachingStrategy.IndexAndCache
                ? PackageOrigin.Mirrored
                : PackageOrigin.Cached;

        public MirrorCachingStrategy Strategy(SurfaceContext surface) => CachingStrategy;

        public bool ShouldPersist(SurfaceContext surface) => CachingStrategy != MirrorCachingStrategy.ProxyOnly;
    }
}
