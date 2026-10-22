using AvantiPoint.Packages.UI.Components.Npm;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NpmPackageSearchTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void InitialQueryString_RestoresSearchAndPaginationState()
    {
        var service = new RecordingBrowseService();
        _context.Services.AddLogging();
        _context.Services.AddSingleton<INpmPackageBrowseService>(service);
        var navigation = _context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("browse/npm?q=%40avantipoint&page=2");

        var component = _context.Render<NpmPackageSearch>();

        component.WaitForAssertion(() =>
        {
            Assert.Equal("@avantipoint", service.LastQuery);
            Assert.Equal(20, service.LastSkip);
            Assert.Contains("Page 2", component.Markup, StringComparison.Ordinal);
            Assert.Equal("@avantipoint", component.Find("input[type=search]").GetAttribute("value"));
            Assert.Equal(
                "/browse/npm/packages/%40avantipoint/widgets",
                component.Find("a").GetAttribute("href"));
        });
    }

    [Fact]
    public void NpmHumanRoutes_AreOutsideRegistryNamespace()
    {
        Assert.Equal("/browse/npm", FeedProtocolPreference.ToHref(FeedProtocolPreference.Npm));
        Assert.Equal("/browse/npm/connect", FeedProtocolPreference.ConnectHref(FeedProtocolPreference.Npm));
        Assert.Equal(FeedProtocolPreference.Npm, FeedProtocolPreference.TryInferFromPath("/browse/npm/packages/example"));
        Assert.Null(FeedProtocolPreference.TryInferFromPath("/npm/feed"));
        Assert.Null(FeedProtocolPreference.TryInferFromPath("/npm/packages"));
    }

    [Fact]
    public void SearchFailure_ShowsActionableErrorAndRetry()
    {
        _context.Services.AddLogging();
        _context.Services.AddSingleton<INpmPackageBrowseService>(new ThrowingBrowseService());

        var component = _context.Render<NpmPackageSearch>();

        component.WaitForAssertion(() =>
        {
            var alert = component.Find("[role=alert]");
            Assert.Contains("couldn't load npm packages", alert.TextContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Try again", component.Markup, StringComparison.Ordinal);
        });
    }

    public void Dispose() => _context.Dispose();

    private sealed class RecordingBrowseService : INpmPackageBrowseService
    {
        public string? LastQuery { get; private set; }

        public int LastSkip { get; private set; }

        public Task<IReadOnlyList<NpmPackageListItem>> SearchAsync(
            string? query,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            LastSkip = skip;
            IReadOnlyList<NpmPackageListItem> results =
            [
                new("@avantipoint/widgets", "1.0.0", DateTime.UtcNow, "Widgets"),
                .. Enumerable.Range(0, 20).Select(index =>
                    new NpmPackageListItem($"package-{index}", "1.0.0", DateTime.UtcNow)),
            ];
            return Task.FromResult(results);
        }

        public Task<NpmPackageDetailModel?> GetPackageAsync(
            string packageName,
            string? detailedVersion = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<NpmPackageDetailModel?>(null);
    }

    private sealed class ThrowingBrowseService : INpmPackageBrowseService
    {
        public Task<IReadOnlyList<NpmPackageListItem>> SearchAsync(
            string? query,
            int skip,
            int take,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Registry unavailable");

        public Task<NpmPackageDetailModel?> GetPackageAsync(
            string packageName,
            string? detailedVersion = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<NpmPackageDetailModel?>(null);
    }
}
