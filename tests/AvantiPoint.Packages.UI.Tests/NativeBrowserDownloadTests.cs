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
