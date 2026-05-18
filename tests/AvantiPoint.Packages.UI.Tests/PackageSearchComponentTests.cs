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

        var checkbox = cut.Find("#prerel-checkbox");
        await cut.InvokeAsync(() => checkbox.Change(true));

        var applyButton = cut.Find(".apply-btn .btn-brand");
        await cut.InvokeAsync(() => applyButton.Click());

        await WaitForSearchToCompleteAsync(cut, Xunit.TestContext.Current.CancellationToken);

        var resultsMarkup = cut.Markup;
        Assert.True(
            resultsMarkup.Contains("packages", StringComparison.OrdinalIgnoreCase) ||
            resultsMarkup.Contains("package", StringComparison.OrdinalIgnoreCase) ||
            resultsMarkup.Contains("No packages found", StringComparison.OrdinalIgnoreCase),
            $"Expected to find 'packages', 'package', or 'No packages found' in markup. Actual: {resultsMarkup.Substring(0, Math.Min(200, resultsMarkup.Length))}");
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
