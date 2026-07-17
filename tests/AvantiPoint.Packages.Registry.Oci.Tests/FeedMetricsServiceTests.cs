using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Feed.Platform.Metrics;
using Prometheus;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class FeedMetricsServiceTests
{
    [Fact]
    public async Task ActionHandler_ExportsLowCardinalityPrometheusMetrics()
    {
        var registry = Metrics.NewCustomRegistry();
        var metrics = new FeedMetricsService(Metrics.WithCustomRegistry(registry));
        var handler = new FeedMetricsActionHandler(metrics);
        var surface = new SurfaceContext(
            "corp",
            FeedProtocol.Npm,
            "npm",
            null,
            "/npm",
            new Uri("https://packages.example.test/npm"));

        await handler.OnArtifactUploaded(
            new FeedArtifactEventContext(surface, "first-package", "1.0.0", null),
            TestContext.Current.CancellationToken);
        await handler.OnArtifactUploaded(
            new FeedArtifactEventContext(surface, "second-package", "2.0.0", null),
            TestContext.Current.CancellationToken);
        await handler.OnArtifactDownloaded(
            new FeedArtifactEventContext(surface, "first-package", "1.0.0", null),
            TestContext.Current.CancellationToken);
        metrics.SetBlobBytes("corp", FeedProtocol.Oci, 2048);

        using var output = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(output, TestContext.Current.CancellationToken);
        var text = Encoding.UTF8.GetString(output.ToArray());

        Assert.Contains("feed_push_total{feed=\"corp\",type=\"npm\"} 2", text);
        Assert.Contains("feed_pull_total{feed=\"corp\",type=\"npm\"} 1", text);
        Assert.Contains("blob_bytes_stored{feed=\"corp\",type=\"oci\"} 2048", text);
        Assert.DoesNotContain("first-package", text);
        Assert.DoesNotContain("second-package", text);
        Assert.Equal(2, metrics.GetPushCounts()["corp:npm"]);
    }
}
