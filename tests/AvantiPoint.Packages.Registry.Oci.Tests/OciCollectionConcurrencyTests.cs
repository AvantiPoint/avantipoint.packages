using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Metrics;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciCollectionConcurrencyTests(OciTestWebApplicationFactory factory) : IClassFixture<OciTestWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SurfaceContext Surface = new("default", FeedProtocol.Oci, "oci", null, "", new Uri("http://localhost"));
    private const string MediaType = "application/vnd.oci.image.manifest.v1+json";

    [Fact]
    public async Task ConcurrentPublisherWaitsForCollection_AndCannotReferenceDeletedContent()
    {
        await factory.EnsureDatabaseMigratedAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "integration-test-key");
        var config = Encoding.UTF8.GetBytes("{\"architecture\":\"arm64\",\"os\":\"linux\"}");
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(config)).ToLowerInvariant();
        var started = await client.PostAsync("/v2/concurrent/blobs/uploads/", null, Ct);
        (await client.PutAsync(started.Headers.Location + "?digest=" + digest, new ByteArrayContent(config), Ct)).EnsureSuccessStatusCode();
        await using var gcScope = factory.Services.CreateAsyncScope();
        var context = gcScope.ServiceProvider.GetRequiredService<IContext>();
        await context.OciBlobs.Where(blob => blob.Digest == digest)
            .ExecuteUpdateAsync(setters => setters.SetProperty(blob => blob.CreatedAt, DateTime.UtcNow.AddDays(-2)), Ct);
        var deleting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = gcScope.ServiceProvider.GetRequiredService<IStorageBackendFactory>();
        var collector = new OciGarbageCollectionService(context, new PausingDeletes(store, deleting, release),
            gcScope.ServiceProvider.GetRequiredService<FeedMetricsService>(), TimeProvider.System, NullLogger<OciGarbageCollectionService>.Instance);
        var collection = Task.Run(() => collector.CollectAsync(Surface, dryRun: false, cancellationToken: Ct), Ct);
        await deleting.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var publishing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publish = Task.Run(async () =>
        {
            await using var publishScope = factory.Services.CreateAsyncScope();
            var service = publishScope.ServiceProvider.GetRequiredService<IOciRegistryService>();
            using var manifest = new MemoryStream(Encoding.UTF8.GetBytes($$"""
                {"schemaVersion":2,"mediaType":"{{MediaType}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","digest":"{{digest}}","size":{{config.Length}}},"layers":[]}
                """));
            publishing.SetResult();
            return await service.PutManifestAsync(Surface, "concurrent", "latest", MediaType, manifest, Ct);
        }, Ct);
        try
        {
            await publishing.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            await Task.Delay(150, Ct);
            Assert.False(publish.IsCompleted);
        }
        finally { release.TrySetResult(); }
        Assert.True((await collection.WaitAsync(TimeSpan.FromSeconds(10), Ct)).Deleted);
        var error = await Assert.ThrowsAsync<OciRegistryException>(async () => await publish.WaitAsync(TimeSpan.FromSeconds(10), Ct));
        Assert.Contains("Referenced blob", error.Message);
        Assert.False(await context.OciTags.AnyAsync(tag => tag.Repository.Name == "concurrent", Ct));
        Assert.False(await context.OciBlobs.AnyAsync(blob => blob.Digest == digest, Ct));
    }

    [Fact]
    public async Task MissingPhysicalBlobCannotBePublishedAfterInterruptedCollection()
    {
        await factory.EnsureDatabaseMigratedAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var digest = "sha256:" + new string('b', 64);
        context.OciBlobs.Add(new() { Digest = digest, Size = 123 });
        await context.SaveChangesAsync(Ct);
        var service = scope.ServiceProvider.GetRequiredService<IOciRegistryService>();
        Assert.False((await service.BlobExistsAsync(Surface, digest, cancellationToken: Ct)).Exists);
        using var manifest = new MemoryStream(Encoding.UTF8.GetBytes($$"""
            {"schemaVersion":2,"mediaType":"{{MediaType}}","config":{"digest":"{{digest}}"},"layers":[]}
            """));
        await Assert.ThrowsAsync<OciRegistryException>(() => service.PutManifestAsync(Surface, "missing", "latest", MediaType, manifest, Ct));
    }

    private sealed class PausingDeletes(IStorageBackendFactory factory, TaskCompletionSource deleting, TaskCompletionSource release) : IStorageBackendFactory
    {
        public IPathBlobStore CreatePathStore(string prefix) => factory.CreatePathStore(prefix);
        public IDigestBlobStore CreateDigestStore(string prefix) => new Store(factory.CreateDigestStore(prefix), deleting, release);
        private sealed class Store(IDigestBlobStore store, TaskCompletionSource deleting, TaskCompletionSource release) : IDigestBlobStore
        {
            public Task PutAsync(string algorithm, string hex, Stream content, CancellationToken token = default) => store.PutAsync(algorithm, hex, content, token);
            public Task<Stream> GetAsync(string algorithm, string hex, CancellationToken token = default) => store.GetAsync(algorithm, hex, token);
            public Task<bool> ExistsAsync(string algorithm, string hex, CancellationToken token = default) => store.ExistsAsync(algorithm, hex, token);
            public async Task DeleteAsync(string algorithm, string hex, CancellationToken token = default)
            {
                deleting.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
                await store.DeleteAsync(algorithm, hex, token);
            }
        }
    }
}
