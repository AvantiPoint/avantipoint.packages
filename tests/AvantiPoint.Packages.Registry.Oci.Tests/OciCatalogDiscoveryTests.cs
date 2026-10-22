using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.Registry.Oci.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciCatalogDiscoveryTests : IClassFixture<OciTestWebApplicationFactory>
{
    private const string ApiKey = "integration-test-key";
    private readonly OciTestWebApplicationFactory _factory;

    public OciCatalogDiscoveryTests(OciTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_ExcludeMirrored_WhenIncludeMirroredInCatalogFalse()
    {
        await _factory.EnsureDatabaseMigratedAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();

        var publishedRepo = await SeedRepositoryAsync(context, "published-repo", PackageOrigin.Published);
        var mirroredRepo = await SeedRepositoryAsync(context, "mirrored-repo", PackageOrigin.Mirrored);
        var cachedRepo = await SeedRepositoryAsync(context, "cached-repo", PackageOrigin.Cached);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        var response = await client.GetAsync("/v2/_catalog", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var repos = json.RootElement.GetProperty("repositories")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        Assert.Contains(publishedRepo, repos);
        Assert.DoesNotContain(mirroredRepo, repos);
        Assert.DoesNotContain(cachedRepo, repos);
    }

    [Theory]
    [InlineData(PackageOrigin.Published, false, true)]
    [InlineData(PackageOrigin.Published, true, true)]
    [InlineData(PackageOrigin.Mirrored, false, false)]
    [InlineData(PackageOrigin.Mirrored, true, true)]
    [InlineData(PackageOrigin.Cached, false, false)]
    [InlineData(PackageOrigin.Cached, true, false)]
    public void DiscoveryPolicy_MatchesCatalogConfiguration(
        PackageOrigin origin,
        bool includeMirrored,
        bool expected)
    {
        var options = new AvantiPoint.Feed.Platform.Configuration.OciFeedOptions
        {
            IncludeMirroredInCatalog = includeMirrored,
        };

        Assert.Equal(expected, OciDiscoveryPolicy.IsVisible(origin, options));
    }

    private static async Task<string> SeedRepositoryAsync(IContext context, string name, PackageOrigin origin)
    {
        var feedId = "default";
        var repo = new OciRepository
        {
            FeedId = feedId,
            OciSegment = null,
            Name = name,
        };
        context.OciRepositories.Add(repo);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.OciTags.Add(new OciTag
        {
            FeedId = feedId,
            OciSegment = null,
            RepositoryKey = repo.Key,
            Tag = "latest",
            ManifestDigest = "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
            Origin = origin,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return name;
    }
}
