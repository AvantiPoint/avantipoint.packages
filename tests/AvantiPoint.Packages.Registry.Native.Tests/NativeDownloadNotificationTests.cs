using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeDownloadNotificationTests
{
    [Fact]
    public async Task FailedStorageReadDoesNotReportASuccessfulDownload()
    {
        var handler = new DownloadNotificationHandler();
        await using var host = await NativeTestHost.StartAsync(realHttp: true, artifactHandler: handler);
        host.Authenticate("writer");
        const string path = "/maven/com/example/sdk/1.0.0/sdk-1.0.0.jar";
        const string body = "download notification fixture";
        using var published = await host.Client.PutAsync(path, new StringContent(body));
        Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        host.Authenticate("reader");
        using var head = await host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(0, handler.Downloads);
        Assert.Equal(body, await host.Client.GetStringAsync(path));
        Assert.Equal(1, handler.Downloads);
        using var scope = host.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var feed = scope.ServiceProvider.GetRequiredService<AvantiPoint.Feed.Platform.IFeedRegistry>().Feed;
        var prefix = string.IsNullOrEmpty(feed.StoragePrefix) ? "native/" : feed.StoragePrefix.TrimEnd('/') + "/native/";
        await storage.DeleteAsync(prefix + "v2/blobs/sha256/" + hash + "/data");
        Assert.NotNull(await Record.ExceptionAsync(() => host.Client.GetStringAsync(path)));
        Assert.Equal(1, handler.Downloads);
    }
}
