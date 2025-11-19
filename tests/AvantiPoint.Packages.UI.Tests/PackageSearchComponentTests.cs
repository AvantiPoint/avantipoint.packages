using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public class PackageSearchComponentTests : IAsyncLifetime
{
    private readonly OpenFeedFactory _factory = new();
    private HttpClient _client = default!;
    private TestContext _ctx = default!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _ctx = new TestContext();

        // Register search service with absolute service index URL
        _ctx.Services.AddNuGetSearchService(options =>
        {
            options.ServiceIndexUrl = _client.BaseAddress!.ToString().TrimEnd('/') + "/v3/index.json";
            options.ConfigureHttpClient = (_, httpClient) =>
            {
                httpClient.BaseAddress = _client.BaseAddress; // ensure relative calls work
            };
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _ctx.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InitialRender_PerformsSearch()
    {
        var cut = _ctx.RenderComponent<PackageSearch>(parameters => parameters
            .Add(p => p.Placeholder, "Search packages...")
        );

        // Allow async search to complete
        await Task.Delay(500);

        // Assert either results list present or 'No packages found' message rendered without error
        Assert.Null(cut.Instance.GetType().GetField("errorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(cut.Instance));

        var resultsMarkup = cut.Markup;
        Assert.Contains("packages found", resultsMarkup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TogglePrerelease_RefreshesResults()
    {
        var cut = _ctx.RenderComponent<PackageSearch>();
        await Task.Delay(500);

        // Find prerelease checkbox
        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.Change(true);
        await Task.Delay(300);

        // Simple assertion: markup still valid and shows results block
        Assert.Contains("packages found", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}