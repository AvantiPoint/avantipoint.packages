using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciUploadLifecycleTests(OciTestWebApplicationFactory factory) : IClassFixture<OciTestWebApplicationFactory>
{
    [Theory]
    [InlineData("0-4")]
    [InlineData("bytes 0-4/9")]
    public async Task UploadStatus_EnforcesRanges_AndAcceptsChunkedFinalBody(string firstRange)
    {
        await factory.EnsureDatabaseMigratedAsync();
        using var client = Client();
        var repository = "lifecycle/" + Guid.NewGuid().ToString("N");
        var start = await client.PostAsync($"/v2/{repository}/blobs/uploads/", null, Ct);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var location = start.Headers.Location!.ToString();
        Assert.False(string.IsNullOrEmpty(start.Headers.GetValues("Docker-Upload-UUID").Single()));
        Assert.Equal("0-0", (await client.GetAsync(location, Ct)).Headers.GetValues("Range").Single());
        using var patch = new HttpRequestMessage(HttpMethod.Patch, location) { Content = new StringContent("hello") };
        patch.Content.Headers.TryAddWithoutValidation("Content-Range", firstRange);
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(patch, Ct)).StatusCode);
        using var outOfOrder = new HttpRequestMessage(HttpMethod.Patch, location) { Content = new StringContent("bad") };
        outOfOrder.Content.Headers.TryAddWithoutValidation("Content-Range", "0-2");
        var rejected = await client.SendAsync(outOfOrder, Ct);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, rejected.StatusCode);
        Assert.Equal("0-4", rejected.Headers.GetValues("Range").Single());
        using var wrongLength = new HttpRequestMessage(HttpMethod.Patch, location) { Content = new StringContent("bad") };
        wrongLength.Content.Headers.TryAddWithoutValidation("Content-Range", "5-6");
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, (await client.SendAsync(wrongLength, Ct)).StatusCode);
        var status = await client.GetAsync(location, Ct);
        Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);
        Assert.Equal("0-4", status.Headers.GetValues("Range").Single());

        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello OCI"))).ToLowerInvariant();
        using var complete = new HttpRequestMessage(HttpMethod.Put, location + "?digest=" + digest) { Content = new ChunkedBody(" OCI") };
        complete.Content.Headers.TryAddWithoutValidation("Content-Range", "5-8");
        var completed = await client.SendAsync(complete, Ct);
        Assert.Equal(HttpStatusCode.Created, completed.StatusCode);
        Assert.Equal("hello OCI", await client.GetStringAsync(completed.Headers.Location!, Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(location, Ct)).StatusCode);
    }

    [Fact]
    public async Task Cancel_IsScoped_AndSurvivesStorageFailureUntilCleanupRetries()
    {
        await factory.EnsureDatabaseMigratedAsync();
        using var client = Client();
        var repository = "cancel/" + Guid.NewGuid().ToString("N");
        var started = await client.PostAsync($"/docker/v2/{repository}/blobs/uploads/", null, Ct);
        var location = started.Headers.Location!.ToString();
        var id = started.Headers.GetValues("Docker-Upload-UUID").Single();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PatchAsync(location, new StringContent("temporary layer"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/helm/v2/{repository}/blobs/uploads/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/docker/v2/other/blobs/uploads/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(location, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(location, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsync(location, new StringContent("late write"), Ct)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var upload = await context.OciUploads.AsNoTracking().SingleAsync(u => u.UploadId == id, Ct);
        Assert.True(upload.IsCancelled);
        var stores = scope.ServiceProvider.GetRequiredService<IStorageBackendFactory>();
        var failing = new OciUploadCleanupService(context, new FailingDeletes(stores), TimeProvider.System, NullLogger<OciUploadCleanupService>.Instance);
        await failing.CleanupAsync(upload.FeedId, TimeSpan.FromHours(24), Ct);
        Assert.True(await context.OciUploads.AnyAsync(u => u.UploadId == id, Ct));
        await scope.ServiceProvider.GetRequiredService<OciUploadCleanupService>().CleanupAsync(upload.FeedId, TimeSpan.FromHours(24), Ct);
        Assert.False(await context.OciUploads.AnyAsync(u => u.UploadId == id, Ct));
        Assert.False(await stores.CreatePathStore(OciSurfaceOptionsBuilder.GetStorageSubPrefix(upload.OciSegment)).ExistsAsync(upload.StoragePath, Ct));
    }

    [Fact]
    public async Task AnonymousReadersCannotInspectOrCancelUploadSessions()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v2/example/blobs/uploads/unknown", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/v2/example/blobs/uploads/unknown", Ct)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentStaleSessionUpdateIsRejectedByTheDatabase()
    {
        await factory.EnsureDatabaseMigratedAsync();
        using var client = Client();
        var repository = "concurrent/" + Guid.NewGuid().ToString("N");
        var started = await client.PostAsync($"/v2/{repository}/blobs/uploads/", null, Ct);
        var id = started.Headers.GetValues("Docker-Upload-UUID").Single();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var stale = await context.OciUploads.SingleAsync(u => u.UploadId == id, Ct);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PatchAsync(started.Headers.Location!, new StringContent("new bytes"), Ct)).StatusCode);
        stale.IsCancelled = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task IdleCleanupPreservesRecentlyActiveSessionsAndOtherFeeds()
    {
        await factory.EnsureDatabaseMigratedAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var feed = "expiry-" + Guid.NewGuid().ToString("N");
        var old = DateTime.UtcNow.AddDays(-2);
        var expired = new OciUpload { FeedId = feed, UploadId = Guid.NewGuid().ToString("N"), RepositoryName = "test", StoragePath = "v2/uploads/missing-expired/data", CreatedAt = old };
        var active = new OciUpload { FeedId = feed, UploadId = Guid.NewGuid().ToString("N"), RepositoryName = "test", StoragePath = "v2/uploads/missing-active/data", CreatedAt = old, LastActivityAt = DateTime.UtcNow };
        var other = new OciUpload { FeedId = feed + "-other", UploadId = Guid.NewGuid().ToString("N"), RepositoryName = "test", StoragePath = "v2/uploads/missing-other/data", CreatedAt = old };
        context.OciUploads.AddRange(expired, active, other);
        await context.SaveChangesAsync(Ct);
        await scope.ServiceProvider.GetRequiredService<OciUploadCleanupService>().CleanupAsync(feed, TimeSpan.FromHours(24), Ct);
        Assert.False(await context.OciUploads.AnyAsync(u => u.Key == expired.Key, Ct));
        Assert.True(await context.OciUploads.AnyAsync(u => u.Key == active.Key && !u.IsCancelled, Ct));
        Assert.True(await context.OciUploads.AnyAsync(u => u.Key == other.Key && !u.IsCancelled, Ct));
    }

    private sealed class ChunkedBody(string content) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(Encoding.UTF8.GetBytes(content)).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class FailingDeletes(IStorageBackendFactory factory) : IStorageBackendFactory
    {
        public IDigestBlobStore CreateDigestStore(string prefix) => factory.CreateDigestStore(prefix);
        public IPathBlobStore CreatePathStore(string prefix) => new FailedDeleteStore(factory.CreatePathStore(prefix));
        private sealed class FailedDeleteStore(IPathBlobStore store) : IPathBlobStore
        {
            public Task PutAsync(string path, Stream content, CancellationToken token = default) => store.PutAsync(path, content, token);
            public Task<Stream> GetAsync(string path, CancellationToken token = default) => store.GetAsync(path, token);
            public Task<bool> ExistsAsync(string path, CancellationToken token = default) => store.ExistsAsync(path, token);
            public Task DeleteAsync(string path, CancellationToken token = default) => throw new IOException("Simulated storage outage");
        }
    }

    private HttpClient Client()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "integration-test-key");
        return client;
    }
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
