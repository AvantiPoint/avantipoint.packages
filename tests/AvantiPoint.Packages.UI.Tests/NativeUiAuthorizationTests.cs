using System.Net;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeUiAuthorizationTests
{
    [Fact]
    public async Task PublishedPubArchiveAppearsInPrivateUiAndDownloadsWithReadToken()
    {
        await using var factory = new NativeUiFactory(anonymous: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IFeedTokenAuthenticationService, NativeUiTokenAuthentication>();
            services.AddSingleton<IFeedActionHandler, NativeUiArtifactPolicy>();
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "writer");
        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
        using (var tar = new System.Formats.Tar.TarWriter(gzip, leaveOpen: true))
        {
            tar.WriteEntry(new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, "pubspec.yaml")
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("name: published_sdk\nversion: 1.0.0\nenvironment:\n  sdk: ^3.0.0\n")),
            });
        }
        var bytes = output.ToArray();
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(bytes), "file", "package.tar.gz");
        using var uploaded = await client.PostAsync("/pub/api/packages/versions/upload", multipart, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, uploaded.StatusCode);
        Assert.StartsWith("https://registry.test/prefix/pub/", uploaded.Headers.Location!.AbsoluteUri);
        // The reverse proxy removes the externally visible /prefix before routing.
        var finalize = uploaded.Headers.Location.PathAndQuery["/prefix".Length..];
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(finalize, Xunit.TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "reader");
        var browse = await client.GetStringAsync("/native/pub", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("published_sdk", browse);
        var detail = await client.GetStringAsync("/native/pub/packages/published_sdk", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("Version 1.0.0", detail);
        Assert.Contains(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), detail);
        Assert.Equal(bytes, await client.GetByteArrayAsync("/pub/packages/published_sdk/versions/1.0.0.tar.gz", Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PrivateMetadataValidatesReadTokensAndArtifactPolicies()
    {
        await using var factory = new NativeUiFactory(anonymous: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IFeedTokenAuthenticationService, NativeUiTokenAuthentication>();
            services.AddSingleton<IFeedActionHandler, NativeUiArtifactPolicy>();
        }));
        using var client = app.CreateClient();
        await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", "visible", "1.0.0");
        await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", "hidden", "1.0.0");
        client.DefaultRequestHeaders.Authorization = new("Bearer", "reader");
        var browse = await client.GetStringAsync("/native/pub", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("visible", browse);
        Assert.DoesNotContain(">hidden<", browse);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/native/pub/packages/visible", Xunit.TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/native/pub/packages/hidden", Xunit.TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "writer");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/native/pub", Xunit.TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        using var denied = await client.GetAsync("/native/pub", Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Contains("fixture", denied.Headers.WwwAuthenticate.ToString());
    }
}
