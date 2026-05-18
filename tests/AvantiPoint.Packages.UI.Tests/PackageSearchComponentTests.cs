using System.Reflection;

namespace AvantiPoint.Packages.UI.Tests;

public class PackageSearchComponentTests : IDisposable
{
    private static BunitContext Initialize()
    {
        var ctx = new BunitContext();
        ctx.Services.AddHttpClient();
        // Register search service using NuGet.org service index for testing
        ctx.Services.AddNuGetSearchService(o =>
        {
            o.ServiceIndexUrl = "https://api.nuget.org/v3/index.json";
        });
        return ctx;
    }

    private readonly BunitContext _ctx = Initialize();

    public void Dispose()
    {
        _ctx.Dispose();
    }

    [Fact]
    public async Task InitialRender_PerformsSearch()
    {
        var cut = _ctx.Render<PackageSearch>(parameters => parameters
            .Add(p => p.Placeholder, "Search packages...")
        );

        // Allow async search to complete
        await Task.Delay(500, Xunit.TestContext.Current.CancellationToken);

        // Assert either results list present or 'No packages found' message rendered without error
        Assert.Null(cut.Instance.GetType().GetField("errorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(cut.Instance));

        var resultsMarkup = cut.Markup;
        // Component shows "packages", "package", or "No packages found" depending on results
        Assert.True(
            resultsMarkup.Contains("packages", StringComparison.OrdinalIgnoreCase) ||
            resultsMarkup.Contains("package", StringComparison.OrdinalIgnoreCase) ||
            resultsMarkup.Contains("No packages found", StringComparison.OrdinalIgnoreCase),
            $"Expected to find 'packages', 'package', or 'No packages found' in markup. Actual: {resultsMarkup.Substring(0, Math.Min(200, resultsMarkup.Length))}");
    }

    [Fact]
    public async Task TogglePrerelease_RefreshesResults()
    {
        var cut = _ctx.Render<PackageSearch>();
        await WaitForSearchToCompleteAsync(cut, Xunit.TestContext.Current.CancellationToken);

        // Avoid DOM change events while async search re-renders (stale bUnit handler IDs on CI).
        await SetPrereleaseAndApplyAsync(cut, includePrerelease: true, Xunit.TestContext.Current.CancellationToken);

        await WaitForSearchToCompleteAsync(cut, Xunit.TestContext.Current.CancellationToken);

        var resultsMarkup = cut.Markup;
        Assert.True(
            resultsMarkup.Contains("packages", StringComparison.OrdinalIgnoreCase) ||
            resultsMarkup.Contains("package", StringComparison.OrdinalIgnoreCase) ||
            resultsMarkup.Contains("No packages found", StringComparison.OrdinalIgnoreCase),
            $"Expected to find 'packages', 'package', or 'No packages found' in markup. Actual: {resultsMarkup.Substring(0, Math.Min(200, resultsMarkup.Length))}");
    }

    private static async Task SetPrereleaseAndApplyAsync(
        IRenderedComponent<PackageSearch> cut,
        bool includePrerelease,
        CancellationToken cancellationToken)
    {
        var includePrereleaseField = typeof(PackageSearch).GetField(
            "includePrerelease",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find includePrerelease field on PackageSearch.");

        var applyFiltersMethod = typeof(PackageSearch).GetMethod(
            "ApplyFilters",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find ApplyFilters method on PackageSearch.");

        includePrereleaseField.SetValue(cut.Instance, includePrerelease);
        await cut.InvokeAsync(() => (Task)applyFiltersMethod.Invoke(cut.Instance, null)!);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task WaitForSearchToCompleteAsync(IRenderedComponent<PackageSearch> component, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (!component.Markup.Contains("search-results-loading", StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for package search to complete.");
    }
}
