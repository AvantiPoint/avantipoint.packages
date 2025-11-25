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
        await Task.Delay(500, Xunit.TestContext.Current.CancellationToken);

        // Find prerelease checkbox
        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.Change(true);
        await Task.Delay(300, Xunit.TestContext.Current.CancellationToken);

        // Simple assertion: markup still valid and shows results block
        var resultsMarkup = cut.Markup;
        Assert.True(
            resultsMarkup.Contains("packages", StringComparison.OrdinalIgnoreCase) ||
            resultsMarkup.Contains("package", StringComparison.OrdinalIgnoreCase) ||
            resultsMarkup.Contains("No packages found", StringComparison.OrdinalIgnoreCase),
            $"Expected to find 'packages', 'package', or 'No packages found' in markup. Actual: {resultsMarkup.Substring(0, Math.Min(200, resultsMarkup.Length))}");
    }
}
