using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciRepositoryBlobAuthorizationTests(OciTestWebApplicationFactory factory) : IClassFixture<OciTestWebApplicationFactory>
{
    [Fact]
    public async Task RepositoryScopedPull_CannotReadOrProbeAnotherRepositorysBlob()
    {
        var ct = TestContext.Current.CancellationToken;
        await factory.EnsureDatabaseMigratedAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var repositoryA = "blob-scope-a/" + suffix;
        var repositoryB = "blob-scope-b/" + suffix;
        var bytes = Encoding.UTF8.GetBytes("synthetic-private-layer-" + suffix);
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
        using var publisher = factory.CreateClient();
        publisher.DefaultRequestHeaders.Authorization = new("Bearer", "integration-test-key");
        using var started = await publisher.PostAsync($"/v2/{repositoryB}/blobs/uploads/", null, ct);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        using var completed = await publisher.PutAsync(started.Headers.Location + "?digest=" + digest, new ByteArrayContent(bytes), ct);
        Assert.Equal(HttpStatusCode.Created, completed.StatusCode);

        using var reader = factory.CreateClient();
        async Task<string> Issue(string repository)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/token?service=default&scope=repository:{repository}:pull");
            request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("user:integration-test-key")));
            using var response = await reader.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return json.RootElement.GetProperty("token").GetString()!;
        }
        reader.DefaultRequestHeaders.Authorization = new("Bearer", await Issue(repositoryA));
        using var leaked = await reader.GetAsync($"/v2/{repositoryA}/blobs/{digest}", ct);
        Assert.Equal(HttpStatusCode.NotFound, leaked.StatusCode);
        using var probe = new HttpRequestMessage(HttpMethod.Head, $"/v2/{repositoryA}/blobs/{digest}");
        using var probed = await reader.SendAsync(probe, ct);
        Assert.Equal(HttpStatusCode.NotFound, probed.StatusCode);

        reader.DefaultRequestHeaders.Authorization = new("Bearer", await Issue(repositoryB));
        Assert.Equal(bytes, await reader.GetByteArrayAsync($"/v2/{repositoryB}/blobs/{digest}", ct));
    }
}
