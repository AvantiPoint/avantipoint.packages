using System.Net.Http.Json;

namespace AvantiPoint.Packages.UI.Tests;

public class SearchApiTests(OpenFeedFactory factory) : IClassFixture<OpenFeedFactory>
{
    [Fact]
    public async Task SearchEndpoint_ReturnsHits()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v3/search?q=Test&take=5", Xunit.TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var search = await response.Content.ReadFromJsonAsync<SearchResponse>(Xunit.TestContext.Current.CancellationToken);
        Assert.NotNull(search);
        Assert.True(search!.TotalHits >= 1, "Expected at least one seeded package to be returned.");
    }

    [Fact]
    public async Task AutocompleteEndpoint_ReturnsPackageIds()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v3/autocomplete?q=Demo&take=5", Xunit.TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var auto = await response.Content.ReadFromJsonAsync<AutocompleteResponse>(Xunit.TestContext.Current.CancellationToken);
        Assert.NotNull(auto);
        Assert.Contains(auto!.Data, id => id.Contains("Demo"));
    }
}
