using System.Net;
using AvantiPoint.Feed.Platform.Callbacks;
using Microsoft.Extensions.DependencyInjection;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeReviewRegressionTests
{
    [Theory]
    [InlineData(".nan")]
    [InlineData(".inf")]
    [InlineData("-.inf")]
    public async Task NonFinitePubspecValuesReturnBadRequestWithoutCommitting(string value)
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        using var archive = PubUpload($"name: example_sdk\nversion: 1.0.0\ncustom:\n  values: [{value}]\n");
        using var response = await host.Client.PostAsync("/pub/api/packages/versions/upload", archive);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_archive", await response.Content.ReadAsStringAsync());
        using var scope = host.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IContext>().NativeArtifacts.ToListAsync());
    }

    [Fact]
    public async Task SwiftRejectsPublicInterfaceForAnotherModuleBeforeCommit()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        const string path = "/swift/example/1.0.0/Example.xcframework.zip";
        using var content = new ByteArrayContent(NativeRegistryTests.Xcframework(interfaceModule: "Other"));
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(path, content)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task SwiftAuthorizationAndDownloadEventsUseLogicalPaths()
    {
        var handler = new LogicalPathArtifactHandler();
        await using var host = await NativeTestHost.StartAsync(artifactHandler: handler);
        host.Authenticate("writer");
        const string path = "/swift/example/1.0.0/Example.xcframework.zip";
        var bytes = NativeRegistryTests.Xcframework();
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(path, new ByteArrayContent(bytes))).StatusCode);
        host.Authenticate("reader");
        Assert.Equal(bytes, await host.Client.GetByteArrayAsync(path));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(new(HttpMethod.Head, path))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/swift/example/1.0.0/index.json")).StatusCode);
        Assert.Equal("example/1.0.0/Example.xcframework.zip", Assert.Single(handler.Uploads).DigestOrTarballPath);
        Assert.Equal("example/1.0.0/Example.xcframework.zip", Assert.Single(handler.Downloads).DigestOrTarballPath);
    }

    [Fact]
    public async Task PubAuthorizationAndEventsUseLogicalPathsThroughoutPublication()
    {
        var handler = new LogicalPathArtifactHandler();
        await using var host = await NativeTestHost.StartAsync(artifactHandler: handler);
        host.Authenticate("writer");
        using var archive = PubUpload("name: example_sdk\nversion: 1.0.0\n");
        using var uploaded = await host.Client.PostAsync("/pub/api/packages/versions/upload", archive);
        Assert.Equal(HttpStatusCode.NoContent, uploaded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(uploaded.Headers.Location)).StatusCode);
        host.Authenticate("reader");
        foreach (var path in new[] { "/pub/api/packages/example_sdk", "/pub/api/packages/example_sdk/versions/1.0.0", "/pub/packages/example_sdk/versions/1.0.0.tar.gz" })
            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(new(HttpMethod.Head, "/pub/packages/example_sdk/versions/1.0.0.tar.gz"))).StatusCode);
        Assert.Equal("packages/example_sdk/versions/1.0.0.tar.gz", Assert.Single(handler.Uploads).DigestOrTarballPath);
        Assert.Equal("packages/example_sdk/versions/1.0.0.tar.gz", Assert.Single(handler.Downloads).DigestOrTarballPath);
    }

    [Theory]
    [InlineData("/pub/api/packages/example_sdk")]
    [InlineData("/pub/api/packages/example_sdk/versions/1.0.0")]
    [InlineData("/pub/packages/example_sdk/versions/1.0.0.tar.gz")]
    [InlineData("/pub/api/packages/example_sdk/versions/1.0.0/finalize?checksum=unused")]
    public async Task PubCallbackDenialsIncludeBearerChallenge(string path)
    {
        var handler = new LogicalPathArtifactHandler { RequireLogicalPath = false };
        await using var host = await NativeTestHost.StartAsync(artifactHandler: handler);
        host.Authenticate("writer");
        using var archive = PubUpload("name: example_sdk\nversion: 1.0.0\n");
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsync("/pub/api/packages/versions/upload", archive)).StatusCode);
        handler.Deny = true;
        using var denied = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("Bearer", denied.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task PubUploadCallbackDenialIncludesBearerChallenge()
    {
        var handler = new LogicalPathArtifactHandler { Deny = true };
        await using var host = await NativeTestHost.StartAsync(artifactHandler: handler);
        host.Authenticate("writer");
        using var archive = PubUpload("name: example_sdk\nversion: 1.0.0\n");
        using var denied = await host.Client.PostAsync("/pub/api/packages/versions/upload", archive);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("Bearer", denied.Headers.WwwAuthenticate.ToString());
    }

    private static MultipartFormDataContent PubUpload(string pubspec)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(NativeRegistryTests.PubTar(pubspec)), "file", "package.tar.gz");
        return content;
    }
}
