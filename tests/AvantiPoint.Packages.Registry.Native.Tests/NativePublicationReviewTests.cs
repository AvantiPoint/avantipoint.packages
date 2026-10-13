using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Registry.Native.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativePublicationReviewTests
{
    [Theory]
    [InlineData("maven")]
    [InlineData("swift")]
    [InlineData("pub")]
    public async Task ConcurrentIdenticalUploadsShareADurableNotificationReceipt(string protocol)
    {
        var handler = new CountingPublicationHandler();
        await using var host = await NativeTestHost.StartAsync(realHttp: true, artifactHandler: handler);
        host.Authenticate("writer");
        var bytes = protocol == "swift" ? NativeRegistryTests.Xcframework()
            : protocol == "pub" ? NativeRegistryTests.PubTar("name: example_sdk\nversion: 1.0.0\n")
            : System.Text.Encoding.UTF8.GetBytes("fixture");
        async Task<HttpResponseMessage> Publish()
        {
            if (protocol != "pub")
            {
                var path = protocol == "swift" ? "/swift/example/1.0.0/Example.xcframework.zip"
                    : "/maven/com/example/sdk/1.0.0/sdk-1.0.0.jar";
                return await host.Client.PutAsync(path, new ByteArrayContent(bytes));
            }
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes), "file", "archive.tar.gz");
            return await host.Client.PostAsync("/pub/api/packages/versions/upload", form);
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Publish()));
        foreach (var result in results)
        {
            using (result) Assert.True(result.IsSuccessStatusCode);
        }
        Assert.Equal(1, handler.Uploads);
        using var scope = host.Services.CreateScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<IContext>().NativeArtifacts.AsNoTracking()
            .SingleAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.True(receipt.UploadCallbackCompleted);
        Assert.Null(receipt.UploadCallbackOwner);
        Assert.Null(receipt.UploadCallbackLeaseUntilUtc);
    }

    [Fact]
    public async Task PubMetadataRetainsUtcAfterDatabaseRoundTrip()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(NativeRegistryTests.PubTar("name: example_sdk\nversion: 1.0.0\n")), "file", "archive.tar.gz");
        Assert.True((await host.Client.PostAsync("/pub/api/packages/versions/upload", form)).IsSuccessStatusCode);
        using var scope = host.Services.CreateScope();
        var artifact = await scope.ServiceProvider.GetRequiredService<IContext>().NativeArtifacts.AsNoTracking()
            .SingleAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(DateTimeKind.Unspecified, artifact.PublishedUtc.Kind);
        host.Authenticate("reader");
        using var metadata = JsonDocument.Parse(await host.Client.GetStringAsync("/pub/api/packages/example_sdk/versions/1.0.0"));
        var published = metadata.RootElement.GetProperty("published").GetString()!;
        Assert.EndsWith("Z", published);
        Assert.Equal(DateTime.SpecifyKind(artifact.PublishedUtc, DateTimeKind.Utc), DateTimeOffset.Parse(published).UtcDateTime);
    }

    [Theory]
    [InlineData(false, 1025)]
    [InlineData(true, 1025)]
    [InlineData(true, 70000)]
    public async Task OversizedMultipartArchivesReturn413(bool chunked, int length)
    {
        await using var host = await NativeTestHost.StartAsync(realHttp: true, maxArtifactBytes: 1024);
        host.Authenticate("writer");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[length]), "file", "archive.tar.gz");
        if (chunked) host.Client.DefaultRequestHeaders.TransferEncodingChunked = true;
        using var response = await host.Client.PostAsync("/pub/api/packages/versions/upload", form);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("archive_too_large", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("maven")]
    [InlineData("swift")]
    [InlineData("pub")]
    public async Task IdempotentRetryRecoversFailedPublicationNotification(string protocol)
    {
        var handler = new RetryPublicationHandler();
        await using var host = await NativeTestHost.StartAsync(realHttp: true, artifactHandler: handler);
        host.Authenticate("writer");
        var swiftBytes = NativeRegistryTests.Xcframework();
        var pubBytes = NativeRegistryTests.PubTar("name: example_sdk\nversion: 1.0.0\n");
        async Task<HttpResponseMessage> Publish()
        {
            if (protocol == "maven") return await host.Client.PutAsync("/maven/com/example/sdk/1.0.0/sdk-1.0.0.jar", new StringContent("fixture"));
            if (protocol == "swift") return await host.Client.PutAsync("/swift/example/1.0.0/Example.xcframework.zip",
                new ByteArrayContent(swiftBytes));
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(pubBytes), "file", "archive.tar.gz");
            return await host.Client.PostAsync("/pub/api/packages/versions/upload", form);
        }
        using var failed = await Publish();
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            var pending = await context.NativeArtifacts.SingleAsync(Xunit.TestContext.Current.CancellationToken);
            pending.UploadCallbackOwner = "expired-process";
            pending.UploadCallbackLeaseUntilUtc = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        }
        using var retried = await Publish();
        Assert.True(retried.IsSuccessStatusCode);
        Assert.Equal(2, handler.Attempts);
        Assert.Equal(1, handler.Completed);
        using var completedRetry = await Publish();
        Assert.True(completedRetry.IsSuccessStatusCode);
        Assert.Equal(2, handler.Attempts);
        Assert.Equal(1, handler.Completed);
    }

    [Fact]
    public async Task Sha256SidecarUsesPersistedDigestWithoutReadingTheBlob()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        const string path = "/maven/com/example/sdk/1.0.0/sdk-1.0.0.jar";
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(path, new StringContent("fixture"))).StatusCode);
        using var scope = host.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IFeedRegistry>();
        var surface = new SurfaceContext(registry.Feed.FeedId, FeedProtocol.Maven, "maven", null, "/maven", new Uri("https://registry.test/maven/"));
        var store = scope.ServiceProvider.GetRequiredService<NativeArtifactStore>();
        var artifact = await store.FindAsync(surface, path["/maven/".Length..], default);
        var md5 = await host.Client.GetStringAsync(path + ".md5");
        var files = scope.ServiceProvider.GetRequiredService<IStorageService>();
        await files.DeleteAsync("native/v2/blobs/sha256/" + artifact!.ContentHash + "/data");
        Assert.Equal(artifact.ContentHash, await host.Client.GetStringAsync(path + ".sha256"));
        Assert.Equal(md5, await host.Client.GetStringAsync(path + ".md5"));
    }
}
