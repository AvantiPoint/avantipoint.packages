using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeAggregateReviewTests
{
    [Theory]
    [InlineData("${revision}")]
    [InlineData("${revision}${sha1}${changelist}")]
    public async Task CiFriendlyProjectsDeployAResolvedCanonicalPom(string sourceVersion)
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        const string path = "/maven/com/example/sdk/1.0.0/sdk-1.0.0.pom";
        var source = "<project><modelVersion>4.0.0</modelVersion><groupId>com.example</groupId><artifactId>sdk</artifactId>"
            + $"<version>{sourceVersion}</version><properties><revision>1.0.0</revision>"
            + "<sha1/><changelist/></properties></project>";
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(path, new StringContent(source))).StatusCode);
        var flattened = source.Replace(sourceVersion, "1.0.0", StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync(path, new StringContent(flattened))).StatusCode);
        host.Authenticate("reader");
        Assert.Equal(flattened, await host.Client.GetStringAsync(path));
        Assert.Contains("<version>1.0.0</version>", await host.Client.GetStringAsync("/maven/com/example/sdk/maven-metadata.xml"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MavenAcceptsCoordinatesInheritedFromDeclaredParent(bool inheritGroup, bool inheritVersion)
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        var pom = "<project xmlns=\"http://maven.apache.org/POM/4.0.0\"><modelVersion>4.0.0</modelVersion>"
            + "<parent><groupId>com.example</groupId><artifactId>parent</artifactId><version>1.0.0</version></parent>"
            + (inheritGroup ? "" : "<groupId>com.example</groupId>")
            + "<artifactId>sdk</artifactId>" + (inheritVersion ? "" : "<version>1.0.0</version>") + "</project>";
        const string path = "/maven/com/example/sdk/1.0.0/sdk-1.0.0.pom";
        using var response = await host.Client.PutAsync(path, new StringContent(pom));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        host.Authenticate("reader");
        Assert.Equal(pom, await host.Client.GetStringAsync(path));
        Assert.Contains("<version>1.0.0</version>", await host.Client.GetStringAsync("/maven/com/example/sdk/maven-metadata.xml"));
    }

    [Theory]
    [InlineData("groupId", "other.example")]
    [InlineData("version", "2.0.0")]
    [InlineData("artifactId", "other")]
    public async Task ExplicitMavenCoordinateMismatchCannotBeOverriddenByParent(string element, string value)
    {
        await using var host = await NativeTestHost.StartAsync();
        host.Authenticate("writer");
        var pom = "<project><parent><groupId>com.example</groupId><artifactId>sdk</artifactId><version>1.0.0</version></parent>"
            + (element == "artifactId" ? "" : "<artifactId>sdk</artifactId>") + $"<{element}>{value}</{element}></project>";
        using var response = await host.Client.PutAsync("/maven/com/example/sdk/1.0.0/sdk-1.0.0.pom", new StringContent(pom));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MavenMetadataAndItsChecksumExposeOnlyAuthorizedCanonicalPoms()
    {
        var policy = new NativeMetadataVisibilityPolicy();
        await using var host = await NativeTestHost.StartAsync(artifactHandler: policy);
        host.Authenticate("writer");
        foreach (var version in new[] { "1.0.0", "2.0.0" })
        {
            var pom = $"<project><groupId>com.example</groupId><artifactId>sdk</artifactId><version>{version}</version></project>";
            using var uploaded = await host.Client.PutAsync($"/maven/com/example/sdk/{version}/sdk-{version}.pom", new StringContent(pom));
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        }
        policy.DeniedPaths.Add("com/example/sdk/2.0.0/sdk-2.0.0.pom");
        host.Authenticate("reader");
        const string path = "/maven/com/example/sdk/maven-metadata.xml";
        var metadata = await host.Client.GetStringAsync(path);
        Assert.Contains("<latest>1.0.0</latest>", metadata);
        Assert.DoesNotContain("2.0.0", metadata);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(metadata))),
            await host.Client.GetStringAsync(path + ".sha256"));
        policy.DeniedPaths.Add("com/example/sdk/1.0.0/sdk-1.0.0.pom");
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task PubVersionListAndLatestExposeOnlyAuthorizedArchives()
    {
        var policy = new NativeMetadataVisibilityPolicy();
        await using var host = await NativeTestHost.StartAsync(artifactHandler: policy);
        host.Authenticate("writer");
        foreach (var version in new[] { "1.0.0", "2.0.0" })
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(NativeRegistryTests.PubTar($"name: example_sdk\nversion: {version}\n")), "file", "package.tar.gz");
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostAsync("/pub/api/packages/versions/upload", form)).StatusCode);
        }
        policy.DeniedPaths.Add("packages/example_sdk/versions/2.0.0.tar.gz");
        host.Authenticate("reader");
        const string path = "/pub/api/packages/example_sdk";
        using var json = JsonDocument.Parse(await host.Client.GetStringAsync(path));
        Assert.Equal("1.0.0", json.RootElement.GetProperty("latest").GetProperty("version").GetString());
        Assert.Single(json.RootElement.GetProperty("versions").EnumerateArray());
        policy.DeniedPaths.Add("packages/example_sdk/versions/1.0.0.tar.gz");
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task SwiftIndexOmitsDeniedModuleAndReturns404WhenAllModulesAreHidden()
    {
        var policy = new NativeMetadataVisibilityPolicy();
        await using var host = await NativeTestHost.StartAsync(artifactHandler: policy);
        host.Authenticate("writer");
        foreach (var module in new[] { "Example", "Other" })
        {
            using var content = new ByteArrayContent(NativeRegistryTests.Xcframework(module: module));
            Assert.Equal(HttpStatusCode.Created, (await host.Client.PutAsync($"/swift/example/1.0.0/{module}.xcframework.zip", content)).StatusCode);
        }
        policy.DeniedPaths.Add("example/1.0.0/Other.xcframework.zip");
        host.Authenticate("reader");
        const string path = "/swift/example/1.0.0/index.json";
        using var json = JsonDocument.Parse(await host.Client.GetStringAsync(path));
        Assert.Equal("Example", Assert.Single(json.RootElement.GetProperty("artifacts").EnumerateArray()).GetProperty("module").GetString());
        policy.DeniedPaths.Add("example/1.0.0/Example.xcframework.zip");
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(path)).StatusCode);
    }
}
