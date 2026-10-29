using System.Net;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class MavenPluginMetadataTests
{
    [Theory]
    [InlineData("com.example", "com/example")]
    [InlineData("example", "example")]
    public async Task PluginDeploymentAcceptsGroupMetadataWithoutReplacingArtifactIndex(string group, string directory)
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        var pom = $"""
            <project>
              <modelVersion>4.0.0</modelVersion>
              <groupId>{group}</groupId>
              <artifactId>sdk-maven-plugin</artifactId>
              <version>1.0.0</version>
              <packaging>maven-plugin</packaging>
            </project>
            """;
        using var artifact = await host.Client.PutAsync($"/maven/{directory}/sdk-maven-plugin/1.0.0/sdk-maven-plugin-1.0.0.pom",
            new StringContent(pom));
        Assert.Equal(HttpStatusCode.Created, artifact.StatusCode);
        using var metadata = await host.Client.PutAsync($"/maven/{directory}/maven-metadata.xml",
            new StringContent(Metadata(group, "sdk-maven-plugin")));
        Assert.Equal(HttpStatusCode.Created, metadata.StatusCode);
        using var checksum = await host.Client.PutAsync($"/maven/{directory}/maven-metadata.xml.sha256",
            new StringContent(new string('a', 64)));
        Assert.Equal(HttpStatusCode.Created, checksum.StatusCode);
        var index = await host.Client.GetStringAsync($"/maven/{directory}/sdk-maven-plugin/maven-metadata.xml");
        Assert.Contains("<version>1.0.0</version>", index);
        Assert.DoesNotContain("<plugins>", index);
        using var artifactHint = await host.Client.PutAsync($"/maven/{directory}/sdk-maven-plugin/maven-metadata.xml",
            new StringContent($"<metadata><groupId>{group}</groupId><artifactId>sdk-maven-plugin</artifactId><versioning><versions><version>9.0.0</version></versions></versioning></metadata>"));
        Assert.Equal(HttpStatusCode.Created, artifactHint.StatusCode);
        Assert.DoesNotContain("9.0.0", await host.Client.GetStringAsync($"/maven/{directory}/sdk-maven-plugin/maven-metadata.xml"));
        using var scope = host.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IContext>().NativeArtifacts
            .CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EveryAdvertisedPluginRequiresArtifactAuthorization()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        var metadata = Metadata("com.example", "sdk-maven-plugin", "denied-plugin");
        using var response = await host.Client.PutAsync("/maven/com/example/maven-metadata.xml",
            new StringContent(metadata));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GroupMetadataAuthorizesTheActualPluginAndLogicalMetadataPath()
    {
        var policy = new MavenPluginMetadataAuthorizationHandler();
        await using var host = await NativeTestHost.StartAsync(artifactHandler: policy);
        host.Authenticate("writer");
        using var response = await host.Client.PutAsync("/maven/com/example/maven-metadata.xml",
            new StringContent(Metadata("com.example", "sdk")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authorization = Assert.Single(policy.Authorizations);
        Assert.Equal("com.example:sdk", authorization.ArtifactName);
        Assert.Null(authorization.Version);
        Assert.Equal("com/example/maven-metadata.xml", authorization.DigestOrTarballPath);
        Assert.Equal(AvantiPoint.Feed.Platform.FeedProtocol.Maven, authorization.Surface.Protocol);
        Assert.Equal("maven", authorization.Surface.SurfaceId);
    }

    [Fact]
    public async Task GroupMetadataRespectsPathAuthorization()
    {
        var policy = new NativeMetadataVisibilityPolicy();
        policy.DeniedPaths.Add("com/example/maven-metadata.xml");
        await using var host = await NativeTestHost.StartAsync(artifactHandler: policy);
        host.Authenticate("writer");
        using var response = await host.Client.PutAsync("/maven/com/example/maven-metadata.xml",
            new StringContent(Metadata("com.example", "sdk")));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ArtifactMetadataStillRequiresArtifactAuthorization()
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        using var response = await host.Client.PutAsync("/maven/com/example/denied/maven-metadata.xml",
            new StringContent("<metadata><groupId>com.example</groupId><artifactId>denied</artifactId></metadata>"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("<metadata><groupId>Com.Example</groupId><plugins><plugin><prefix>sdk</prefix><artifactId>sdk</artifactId></plugin></plugins></metadata>")]
    [InlineData("<metadata><groupId>com.example</groupId><versioning /><plugins><plugin><prefix>sdk</prefix><artifactId>sdk</artifactId></plugin></plugins></metadata>")]
    [InlineData("<metadata><groupId>other</groupId><plugins><plugin><prefix>sdk</prefix><artifactId>sdk</artifactId></plugin></plugins></metadata>")]
    [InlineData("<metadata><groupId>com.example</groupId><plugins /></metadata>")]
    [InlineData("<metadata><groupId>com.example</groupId><plugins><plugin><prefix>sdk</prefix><artifactId>../sdk</artifactId></plugin></plugins></metadata>")]
    [InlineData("<metadata><groupId>com.example</groupId><plugins><plugin><artifactId>sdk</artifactId></plugin></plugins></metadata>")]
    [InlineData("<metadata><groupId>com.example</groupId><artifactId>sdk</artifactId><plugins><plugin><prefix>sdk</prefix><artifactId>sdk</artifactId></plugin></plugins></metadata>")]
    [InlineData("<!DOCTYPE metadata [<!ENTITY group 'com.example'>]><metadata><groupId>&group;</groupId><plugins /></metadata>")]
    public async Task InvalidGroupMetadataIsRejected(string metadata)
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        using var response = await host.Client.PutAsync("/maven/com/example/maven-metadata.xml",
            new StringContent(metadata));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("reader", HttpStatusCode.Forbidden)]
    public async Task GroupMetadataStillRequiresPushAuthentication(string? token, HttpStatusCode expected)
    {
        await using var host = await NativeTestHost.StartAsync(anonymous: true);
        if (token is not null) host.Authenticate(token);
        using var response = await host.Client.PutAsync("/maven/com/example/maven-metadata.xml",
            new StringContent(Metadata("com.example", "sdk")));
        Assert.Equal(expected, response.StatusCode);
    }

    private static string Metadata(string group, params string[] artifacts) =>
        new XElement("metadata",
            new XElement("groupId", group),
            new XElement("plugins", artifacts.Select(artifact =>
                new XElement("plugin",
                    new XElement("name", "SDK plugin"),
                    new XElement("prefix", artifact),
                    new XElement("artifactId", artifact)))))
            .ToString(SaveOptions.DisableFormatting);
}
