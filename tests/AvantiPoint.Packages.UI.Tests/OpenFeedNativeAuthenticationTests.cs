using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TestContext = Xunit.TestContext;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class OpenFeedNativeAuthenticationTests
{
    [Theory]
    [InlineData("/maven/com/example/sdk/1.0.0/sdk-1.0.0.pom", "Basic")]
    [InlineData("/swift/example/1.0.0/Example.xcframework.zip", "Basic")]
    [InlineData("/pub/api/packages/versions/upload", "Bearer")]
    public async Task ConfiguredSampleKeyAuthenticatesNativePublication(string path, string scheme)
    {
        await using var factory = new NativeUiFactory(anonymous: false, apiKey: "sample-test-key");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Credentials(scheme, "invalid-key");
        using var invalid = await PublishAsync(client, path);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        client.DefaultRequestHeaders.Authorization = Credentials(scheme, "sample-test-key");
        using var response = await PublishAsync(client, path);
        // A POM is actually committed; the invalid ZIP/gzip bodies reach their
        // format validators instead of failing because DI authentication is absent.
        Assert.Equal(path.StartsWith("/maven/", StringComparison.Ordinal)
            ? HttpStatusCode.Created : HttpStatusCode.BadRequest, response.StatusCode);
        if (path.StartsWith("/maven/", StringComparison.Ordinal))
        {
            var downloaded = await client.GetStringAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("<artifactId>sdk</artifactId>", downloaded);
        }
    }

    [Theory]
    [InlineData("/maven/com/example/sdk/1.0.0/sdk-1.0.0.pom", "Basic")]
    [InlineData("/swift/example/1.0.0/Example.xcframework.zip", "Basic")]
    [InlineData("/pub/api/packages/example_sdk", "Bearer")]
    public async Task PrivateSamplePullUsesConfiguredKey(string path, string scheme)
    {
        await using var factory = new NativeUiFactory(anonymous: false, apiKey: "sample-test-key");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Credentials(scheme, "invalid-key");
        using var invalid = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        client.DefaultRequestHeaders.Authorization = Credentials(scheme, "sample-test-key");
        using var authorized = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, authorized.StatusCode);
    }

    [Fact]
    public async Task SampleWithoutConfiguredKeyDoesNotAllowArbitraryNativePublishToken()
    {
        await using var factory = new NativeUiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Credentials("Basic", "arbitrary-token");
        using var response = await PublishAsync(client, "/maven/com/example/sdk/1.0.0/sdk-1.0.0.pom");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static AuthenticationHeaderValue Credentials(string scheme, string token) => new(scheme,
        scheme == "Basic" ? Convert.ToBase64String(Encoding.UTF8.GetBytes("sample-user:" + token)) : token);

    private static async Task<HttpResponseMessage> PublishAsync(HttpClient client, string path)
    {
        if (path.StartsWith("/pub/", StringComparison.Ordinal))
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("invalid gzip"), "file", "package.tar.gz");
            return await client.PostAsync(path, form, TestContext.Current.CancellationToken);
        }
        var content = path.StartsWith("/maven/", StringComparison.Ordinal)
            ? "<project><modelVersion>4.0.0</modelVersion><groupId>com.example</groupId><artifactId>sdk</artifactId><version>1.0.0</version></project>"
            : "invalid ZIP";
        using var body = new StringContent(content);
        return await client.PutAsync(path, body, TestContext.Current.CancellationToken);
    }
}
