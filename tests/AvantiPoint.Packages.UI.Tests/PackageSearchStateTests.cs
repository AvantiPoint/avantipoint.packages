using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class PackageSearchStateTests : IDisposable
{
    private readonly BunitContext _context = new();

    public PackageSearchStateTests()
    {
        _context.Services.AddLogging();
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void InitialQueryString_IsRestoredAndSentAsTheCompleteFrameworkFilter()
    {
        var search = new RecordingSearchService();
        _context.Services.AddSingleton<INuGetSearchService>(search);
        _context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo(
            "?q=example&prerelease=true&packageType=dependency&frameworks=net8.0,netstandard2.0&includeCompatibleFrameworks=false&frameworkFilterMode=any&page=2");

        var component = _context.Render<PackageSearch>();

        component.WaitForAssertion(() =>
        {
            Assert.NotNull(search.LastRequest);
            var request = search.LastRequest!;
            Assert.Equal("example", request.Query);
            Assert.True(request.IncludePrerelease);
            Assert.Equal("Dependency", request.PackageType);
            Assert.Equal(["net8.0", "netstandard2.0"], request.Frameworks);
            Assert.False(request.IncludeCompatibleFrameworks);
            Assert.Equal(FrameworkFilterMode.Any, request.FrameworkFilterMode);
            Assert.Equal(20, request.Skip);
        });
    }

    [Fact]
    public void AdvancedFilters_StartCollapsedOnNarrowScreensButRemainDesktopVisible()
    {
        _context.Services.AddSingleton<INuGetSearchService>(new RecordingSearchService());

        var component = _context.Render<PackageSearch>();

        var toggle = component.Find("button[aria-controls='advancedSearchPanel']");
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        var panel = component.Find("#advancedSearchPanel");
        Assert.Contains("hidden", panel.ClassList);
        Assert.Contains("md:block", panel.ClassList);
    }

    public void Dispose() => _context.Dispose();

    private sealed class RecordingSearchService : INuGetSearchService
    {
        public SearchRequest? LastRequest { get; private set; }

        public Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(EmptyResponse());
        }

        public Task<SearchResponse> SearchAsync(
            string? query = null,
            int skip = 0,
            int take = 20,
            bool includePrerelease = false,
            string? packageType = null,
            string? framework = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(EmptyResponse());

        public Task<IReadOnlyList<string>> AutocompleteAsync(
            string? query = null,
            int skip = 0,
            int take = 10,
            bool includePrerelease = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        private static SearchResponse EmptyResponse() => new()
        {
            Data = [],
            TotalHits = 0,
        };
    }
}
