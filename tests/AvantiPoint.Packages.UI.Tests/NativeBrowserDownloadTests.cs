using System.Net;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;
using TestContext = Xunit.TestContext;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeBrowserDownloadTests
{
    [Fact]
    public async Task BrowserDownloadsNotifyOnlyAfterStorageTransferSucceeds()
    {
        var handler = new CountingNativeUiHandler();
        await using var factory = new ManagedHostFactory(anonymous: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddAuthentication("Cookies").AddCookie("Cookies");
            services.AddSingleton<IStartupFilter, CaptureCookieStartupFilter>();
            services.AddSingleton<IFeedActionHandler>(handler);
        }));
        using var client = app.CreateClient();
        var bytes = "browser download fixture"u8.ToArray();
        const string path = "packages/sdk/versions/1.0.0.tar.gz";
        string prefix;
        using (var scope = app.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IFeedRegistry>();
            prefix = string.IsNullOrEmpty(registry.Feed.StoragePrefix) ? "native/"
                : registry.Feed.StoragePrefix.TrimEnd('/') + "/native/";
            var surface = new SurfaceContext(registry.Feed.FeedId, FeedProtocol.Pub, "pub", null,
                "/pub", new Uri("https://registry.test/pub/"));
            await using var upload = await ArtifactUpload.ReadAsync(new MemoryStream(bytes), 1048576, TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<NativeArtifactStore>().PutAsync(surface, path, "sdk", "1.0.0",
                "application/octet-stream", upload, "{}", TestContext.Current.CancellationToken);
        }
        await client.GetAsync("/__capture/login?role=reader");
        var download = "/native/pub/download?package=sdk&path=" + Uri.EscapeDataString(path);
        Assert.Equal(bytes, await client.GetByteArrayAsync(download));
        Assert.Equal(1, handler.Downloads);
        using (var scope = app.Services.CreateScope())
        {
            var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
            await scope.ServiceProvider.GetRequiredService<AvantiPoint.Packages.Core.IStorageService>()
                .DeleteAsync(prefix + "v2/blobs/sha256/" + hash + "/data");
        }
        Assert.NotNull(await Record.ExceptionAsync(() => client.GetStringAsync(download)));
        Assert.Equal(1, handler.Downloads);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("consume_removed")]
    [InlineData("approval_removed")]
    [InlineData("deleted")]
    public async Task ExistingConsumerCookieCannotBrowseAfterAccessIsRemoved(string change)
    {
        await using var factory = new ManagedHostFactory(anonymous: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddAuthentication("Cookies").AddCookie("Cookies");
            services.AddSingleton<IStartupFilter, CaptureCookieStartupFilter>();
        }));
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", "private_sdk", "1.0.0");
        await client.GetAsync("/__capture/login?role=reader");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/native/pub/packages/private_sdk")).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IHostIdentityContext>();
            var user = await db.HostUsers.SingleAsync();
            if (change == "revoked") user.IsRevoked = true;
            if (change == "consume_removed") user.CanConsume = false;
            if (change == "approval_removed") user.ApprovalStatus = AvantiPoint.Packages.Host.Admin.Entities.HostUserApprovalStatus.Pending;
            if (change == "deleted") db.HostUsers.Remove(user);
            await db.SaveChangesAsync();
        }
        foreach (var path in new[] { "/native/pub", "/native/pub/packages/private_sdk", "/native/pub/feed" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("private_sdk", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task BrowserDownloadsHonorArtifactPolicyAndRejectTraversal()
    {
        await using var factory = new ManagedHostFactory(anonymous: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddAuthentication("Cookies").AddCookie("Cookies");
            services.AddSingleton<IStartupFilter, CaptureCookieStartupFilter>();
            services.AddSingleton<IFeedActionHandler, NativeCatalogVisibilityPolicy>();
        }));
        using var client = app.CreateClient();
        await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", "hidden_sdk", "1.0.0");
        await client.GetAsync("/__capture/login?role=reader");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            "/native/pub/download?package=hidden_sdk&path=packages%2Fhidden_sdk%2Fversions%2F1.0.0.tar.gz")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            "/native/pub/download?package=hidden_sdk&path=..%2Ffeed-settings.json")).StatusCode);
    }

    [Theory]
    [InlineData("Maven", "com.example:sdk", "com/example/sdk/1.0.0/sdk-1.0.0.pom")]
    [InlineData("Swift", "sdk", "sdk/1.0.0/Example.xcframework.zip")]
    [InlineData("Pub", "sdk", "packages/sdk/versions/1.0.0.tar.gz")]
    public async Task ConsumerCookieDownloadsThroughUiWhileSdkEndpointStillRequiresToken(string protocol, string package, string path)
    {
        await using var factory = new ManagedHostFactory(anonymous: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddAuthentication("Cookies").AddCookie("Cookies");
            services.AddSingleton<IStartupFilter, CaptureCookieStartupFilter>();
        }));
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var bytes = Encoding.UTF8.GetBytes("Synthetic artifact for authenticated browser download.");
        using (var scope = app.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IFeedRegistry>();
            var kind = Enum.Parse<FeedProtocol>(protocol);
            var surface = new SurfaceContext(registry.Feed.FeedId, kind, protocol.ToLowerInvariant(), null,
                "/" + protocol.ToLowerInvariant(), new Uri("https://registry.test/"));
            await using var upload = await ArtifactUpload.ReadAsync(new MemoryStream(bytes), 1048576, TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<NativeArtifactStore>().PutAsync(surface, path, package, "1.0.0",
                "application/octet-stream", upload, "{\"name\":\"sdk\",\"version\":\"1.0.0\"}", TestContext.Current.CancellationToken);
        }
        var route = protocol.ToLowerInvariant();
        var download = $"/native/{route}/download?package={Uri.EscapeDataString(package)}&path={Uri.EscapeDataString(path)}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(download)).StatusCode);
        await client.GetAsync("/__capture/login?role=reader");
        var detail = await client.GetStringAsync($"/native/{route}/packages/{Uri.EscapeDataString(package)}");
        Assert.Contains($"/native/{route}/download?", detail);
        using var response = await client.GetAsync(download);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/{route}/{path}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(download.Replace(Uri.EscapeDataString(path), "missing"))).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IHostIdentityContext>();
            var user = await db.HostUsers.SingleAsync();
            user.IsRevoked = true;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(download)).StatusCode);
        await client.GetAsync("/__capture/login?role=publisher");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(download)).StatusCode);
    }

    [Fact]
    public async Task DisabledBrowserDownloadDoesNotResolveNativeStorage()
    {
        await using var factory = new ManagedHostFactory(enabled: false);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/native/pub/download?package=sdk&path=unused")).StatusCode);
    }
}
