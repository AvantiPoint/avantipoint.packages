using System.Net.Http.Json;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.UI.Tests;

public class SearchApiTests : IClassFixture<OpenFeedFactory>
{
    private readonly OpenFeedFactory _factory;

    public SearchApiTests(OpenFeedFactory factory) => _factory = factory;

    [Fact]
    public async Task SearchEndpoint_ReturnsHits()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v3/search?q=Test&take=5");
        response.EnsureSuccessStatusCode();
        var search = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(search);
        Assert.True(search!.TotalHits >= 1, "Expected at least one seeded package to be returned.");
    }

    [Fact]
    public async Task AutocompleteEndpoint_ReturnsPackageIds()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v3/autocomplete?q=Demo&take=5");
        response.EnsureSuccessStatusCode();
        var auto = await response.Content.ReadFromJsonAsync<AutocompleteResponse>();
        Assert.NotNull(auto);
        Assert.Contains(auto!.Data, id => id.Contains("Demo"));
    }
}