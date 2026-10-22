using AvantiPoint.Packages.UI.Components.Oci;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class HelmChartComparisonBuilderTests
{
    [Fact]
    public void Compare_ReportsConfigurationChangesWithBoundedContext()
    {
        var previous = CreateDetails(
            values: "replicaCount: 1\nimage:\n  tag: 1.0.0\n",
            chart: "apiVersion: v2\nname: platform\nversion: 1.0.0\n",
            schema: null,
            chartLock: "dependencies: []\n");
        var current = CreateDetails(
            values: "replicaCount: 2\nimage:\n  tag: 1.1.0\n",
            chart: "apiVersion: v2\nname: platform\nversion: 1.1.0\n",
            schema: "{\n  \"type\": \"object\"\n}\n",
            chartLock: null);

        var comparison = HelmChartComparisonBuilder.Compare("1.0.0", previous, "1.1.0", current);

        Assert.True(comparison.HasChanges);
        Assert.Equal(4, comparison.ChangedDocumentCount);
        Assert.Equal("1.0.0", comparison.PreviousVersion);
        Assert.Equal("1.1.0", comparison.CurrentVersion);
        var values = Assert.Single(comparison.Documents, document => document.FileName == "values.yaml");
        Assert.Equal("changed", values.ChangeKind);
        Assert.Equal(2, values.AddedLineCount);
        Assert.Equal(2, values.RemovedLineCount);
        Assert.Contains(values.Lines, line => line.Kind == "del" && line.Text == "replicaCount: 1");
        Assert.Contains(values.Lines, line => line.Kind == "add" && line.Text == "replicaCount: 2");
        Assert.Equal("added", Assert.Single(comparison.Documents, document => document.FileName == "values.schema.json").ChangeKind);
        Assert.Equal("removed", Assert.Single(comparison.Documents, document => document.FileName == "Chart.lock").ChangeKind);
    }

    [Fact]
    public void Compare_DoesNotBuildAnUnboundedInlineDiff()
    {
        var previous = CreateDetails("value: old\n", "apiVersion: v2\nname: platform\n", null, null);
        var current = CreateDetails(
            string.Join('\n', Enumerable.Range(0, 1600).Select(index => $"setting{index}: true")),
            "apiVersion: v2\nname: platform\n",
            null,
            null);

        var comparison = HelmChartComparisonBuilder.Compare("1.0.0", previous, "2.0.0", current);

        var values = Assert.Single(comparison.Documents, document => document.FileName == "values.yaml");
        Assert.True(values.PreviewUnavailable);
        Assert.Empty(values.Lines);
        Assert.Null(values.AddedLineCount);
        Assert.Null(values.RemovedLineCount);
    }

    [Fact]
    public void Component_ExplainsSafeUpgradeReviewAndEncodesUntrustedLines()
    {
        using var context = new BunitContext();
        var previous = CreateDetails(
            "message: safe\n",
            "apiVersion: v2\nname: platform\nversion: 1.0.0\n",
            null,
            null);
        var current = CreateDetails(
            "message: <script>alert('x')</script>\n",
            "apiVersion: v2\nname: platform\nversion: 1.1.0\n",
            null,
            null);
        var comparison = HelmChartComparisonBuilder.Compare("1.0.0", previous, "1.1.0", current);

        var component = context.Render<OciHelmChartComparison>(parameters => parameters
            .Add(parameter => parameter.Comparison, comparison));

        Assert.Contains("Upgrade changes", component.Markup, StringComparison.Ordinal);
        Assert.Contains("1.0.0 → 1.1.0", component.Markup, StringComparison.Ordinal);
        Assert.Contains("does not render or execute templates", component.Markup, StringComparison.Ordinal);
        Assert.Contains("2 files changed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert('x')&lt;/script&gt;", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert('x')</script>", component.Markup, StringComparison.Ordinal);
        Assert.Single(component.FindAll("details[open]"));
    }

    private static HelmChartDetails CreateDetails(
        string? values,
        string? chart,
        string? schema,
        string? chartLock) => new(
        "platform",
        null,
        null,
        null,
        "v2",
        null,
        "application",
        null,
        null,
        false,
        [],
        [],
        [],
        [],
        null,
        values,
        schema,
        chart,
        chartLock,
        false,
        null);
}
