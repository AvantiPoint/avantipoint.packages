using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciManifestValidationTests(OciTestWebApplicationFactory factory) : IClassFixture<OciTestWebApplicationFactory>
{
    private const string MediaType = "application/vnd.oci.image.manifest.v1+json";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OversizedManifestIsRejectedBeforeDeserializing()
    {
        using var client = Client();
        using var body = new ByteArrayContent(new byte[OciRegistryService.MaximumManifestBytes + 1]);
        body.Headers.ContentType = new MediaTypeHeaderValue(MediaType);
        var response = await client.PutAsync("/v2/validation/manifests/latest", body, Ct);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("SIZE_INVALID", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"config\":false}")]
    [InlineData("{\"layers\":{}}")]
    [InlineData("{\"layers\":[{\"digest\":3}]}")]
    [InlineData("{\"config\":{\"digest\":\"invalid\"}}")]
    public async Task InvalidDescriptorTypesReturnProtocolErrors(string content)
    {
        await factory.EnsureDatabaseMigratedAsync();
        using var client = Client();
        var response = await client.PutAsync("/v2/validation/manifests/latest", new StringContent(content, Encoding.UTF8, MediaType), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("MANIFEST_INVALID", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task SuppliedManifestDigestMustMatchTheBytes()
    {
        await factory.EnsureDatabaseMigratedAsync();
        using var client = Client();
        var response = await client.PutAsync("/v2/validation/manifests/sha256:" + new string('a', 64),
            new StringContent("{\"schemaVersion\":2,\"manifests\":[]}", Encoding.UTF8, "application/vnd.oci.image.index.v1+json"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("DIGEST_INVALID", await response.Content.ReadAsStringAsync(Ct));
    }

    private HttpClient Client()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "integration-test-key");
        return client;
    }
}
