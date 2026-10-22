using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciPaginationTests(OciTestWebApplicationFactory factory) : IClassFixture<OciTestWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TagPagesAreAsciiOrdered_Scoped_AndExcludeOnlyInternalTags()
    {
        await factory.EnsureDatabaseMigratedAsync();
        var name = "pages/" + Guid.NewGuid().ToString("N");
        string[] visible = ["0.1.0", "ABC", "Z", "a", "sha256-" + new string('a', 63) + "z", "z"];
        await Seed(name, "docker", visible.Concat(["sha256-" + new string('a', 64), "SHA512-" + new string('F', 64), "sha256:abc"]));
        await Seed(name, "helm", ["wrong-surface"]);
        using var client = Client();
        var collected = new List<string>();
        string? url = $"/docker/v2/{name}/tags/list?n=2";
        while (url is not null)
        {
            var response = await client.GetAsync(url, Ct);
            response.EnsureSuccessStatusCode();
            var tags = await Read(response, "tags");
            Assert.Equal(2, tags.Length);
            collected.AddRange(tags);
            url = Next(response);
            if (url is not null) Assert.Contains("/docker/v2/", url);
            Assert.True(collected.Count <= visible.Length);
        }
        Assert.Equal(visible, collected);
        var empty = await client.GetAsync($"/docker/v2/{name}/tags/list?n=0", Ct);
        Assert.Empty(await Read(empty, "tags"));
        Assert.Null(Next(empty));
    }

    [Fact]
    public async Task CatalogPagesSkipHiddenRepositories_AndRespectLastAndZero()
    {
        await factory.EnsureDatabaseMigratedAsync();
        var prefix = "pagination-" + Guid.NewGuid().ToString("N");
        await Seed(prefix + "/a", "docker", ["1"]);
        await Seed(prefix + "/b", "docker", ["sha256-" + new string('a', 64)]);
        await Seed(prefix + "/c", "docker", ["2"]);
        await Seed(prefix + "/other", "helm", ["1"]);
        using var client = Client();
        var response = await client.GetAsync($"/docker/v2/_catalog?n=1&last={prefix}", Ct);
        Assert.Equal([prefix + "/a"], await Read(response, "repositories"));
        Assert.NotNull(Next(response));
        response = await client.GetAsync(Next(response), Ct);
        Assert.Equal([prefix + "/c"], await Read(response, "repositories"));
        var empty = await client.GetAsync("/docker/v2/_catalog?n=0", Ct);
        Assert.Empty(await Read(empty, "repositories"));
        Assert.Null(Next(empty));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("1&n=2")]
    [InlineData("2147483648")]
    public async Task InvalidPageSizesReturnProtocolErrors(string value)
    {
        using var client = Client();
        foreach (var path in new[] { "/v2/_catalog", "/v2/example/tags/list" })
        {
            var response = await client.GetAsync(path + "?n=" + value, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("PAGINATION_NUMBER_INVALID", await response.Content.ReadAsStringAsync(Ct));
        }
    }

    [Fact]
    public async Task DefaultAndExcessivePageSizesAreBounded()
    {
        await factory.EnsureDatabaseMigratedAsync();
        var name = "bounded/" + Guid.NewGuid().ToString("N");
        await Seed(name, null, Enumerable.Range(0, 1003).Select(i => $"v{i:D4}"));
        using var client = Client();
        var response = await client.GetAsync($"/v2/{name}/tags/list", Ct);
        Assert.Equal(100, (await Read(response, "tags")).Length);
        Assert.NotNull(Next(response));
        response = await client.GetAsync($"/v2/{name}/tags/list?n=999999", Ct);
        Assert.Equal(1000, (await Read(response, "tags")).Length);
        Assert.NotNull(Next(response));
        response = await client.GetAsync(Next(response), Ct);
        Assert.Equal(3, (await Read(response, "tags")).Length);
        Assert.Null(Next(response));
    }

    private async Task Seed(string name, string? segment, IEnumerable<string> tags)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var repository = new OciRepository { Name = name, OciSegment = segment };
        context.OciRepositories.Add(repository);
        await context.SaveChangesAsync(Ct);
        context.OciTags.AddRange(tags.Select(tag => new OciTag
        {
            RepositoryKey = repository.Key, Tag = tag, OciSegment = segment,
            Origin = PackageOrigin.Published, ManifestDigest = "sha256:" + new string('a', 64),
        }));
        await context.SaveChangesAsync(Ct);
    }

    private static async Task<string[]> Read(HttpResponseMessage response, string name)
    {
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToArray();
    }
    private static string? Next(HttpResponseMessage response) => response.Headers.TryGetValues("Link", out var links)
        ? links.Single().Split('>')[0].TrimStart('<') : null;
    private HttpClient Client()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "integration-test-key");
        return client;
    }
}
