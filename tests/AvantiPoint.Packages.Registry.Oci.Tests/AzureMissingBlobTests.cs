using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Packages.Core;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class AzureMissingBlobTests : IClassFixture<OciTestWebApplicationFactory>
{
    private readonly OciTestWebApplicationFactory _factory;
    public AzureMissingBlobTests(OciTestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("docker")]
    [InlineData("helm")]
    public async Task UploadLimit_IsScopedAndRejectsOversizedRequests(string segment)
    {
        await _factory.EnsureDatabaseMigratedAsync();
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<OciFeedOptions>(OciSurfaceOptionsBuilder.GetOptionsName(segment), options => options.MaxUploadRequestBytes = 4)));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "integration-test-key");
        var ct = TestContext.Current.CancellationToken;
        var start = await client.PostAsync($"/{segment}/v2/limit/{Guid.NewGuid():N}/blobs/uploads/", null, ct);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var response = await client.PatchAsync(start.Headers.Location, new ByteArrayContent(new byte[5]), ct);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("SIZE_INVALID", await response.Content.ReadAsStringAsync(ct));
    }

    [Theory]
    [InlineData("docker")]
    [InlineData("helm")]
    public async Task MissingAzureBlob_DoesNotPreventUploadOrReturnEmptySuccess(string segment)
    {
        await _factory.EnsureDatabaseMigratedAsync();
        var storage = new AzureLikeStorage();
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(storage);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "integration-test-key");
        var ct = TestContext.Current.CancellationToken;
        var repository = $"missing/{Guid.NewGuid():N}";
        byte[] content = Encoding.UTF8.GetBytes("a real OCI blob");
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var path = $"/{segment}/v2/{repository}/blobs/{digest}";
        var start = await client.PostAsync($"/{segment}/v2/{repository}/blobs/uploads/", null, ct);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var upload = await client.PutAsync(start.Headers.Location + "?digest=" + digest, new ByteArrayContent(content), ct);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        Assert.Equal(content, await client.GetByteArrayAsync(path, ct));

        // Keep the database record but remove its blob, as with a missing Azure object.
        storage.Content.Clear();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, path), ct)).StatusCode);

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var repo = await context.OciRepositories.SingleAsync(r => r.Name == repository && r.OciSegment == segment, ct);
        context.OciManifests.Add(new OciManifest { FeedId = repo.FeedId, OciSegment = segment, Digest = digest,
            MediaType = "application/vnd.oci.image.manifest.v1+json", Size = content.Length });
        context.OciTags.Add(new OciTag { FeedId = repo.FeedId, OciSegment = segment, RepositoryKey = repo.Key,
            Tag = "missing", ManifestDigest = digest });
        await context.SaveChangesAsync(ct);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/{segment}/v2/{repository}/manifests/missing", ct)).StatusCode);
    }

    private sealed class AzureLikeStorage : IStorageService
    {
        public Dictionary<string, byte[]> Content { get; } = [];
        public Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(Content.TryGetValue(path, out var bytes) ? new MemoryStream(bytes) : Stream.Null);
        public async Task<StoragePutResult> PutAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            Content[path] = memory.ToArray();
            return StoragePutResult.Success;
        }
        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) { Content.Remove(path); return Task.CompletedTask; }
        public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<StorageFileInfo> ListFilesAsync(string prefix, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
