using AvantiPoint.Packages.UI.Components.Oci;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class OciDockerImageComparisonBuilderTests
{
    private const string BaseDigest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string PreviousDigest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string CurrentDigest = "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public void Compare_ReportsRuntimeMetadataPlatformAndLayerChanges()
    {
        var previous = CreateDetails(
            command: ["App.dll"],
            labels: new Dictionary<string, string>
            {
                ["org.opencontainers.image.revision"] = "abc123",
                ["removed"] = "old",
            },
            annotations: new Dictionary<string, string>
            {
                ["org.opencontainers.image.description"] = "Old image",
            },
            layers:
            [
                new OciDockerLayer(1, BaseDigest, null, 1024),
                new OciDockerLayer(2, PreviousDigest, null, 2048),
            ],
            platforms:
            [
                new OciDockerPlatform(PreviousDigest, "linux", "amd64", null, 3072),
            ]);
        var current = CreateDetails(
            command: ["App.dll", "--serve"],
            labels: new Dictionary<string, string>
            {
                ["org.opencontainers.image.revision"] = "def456",
                ["added"] = "new",
            },
            annotations: new Dictionary<string, string>
            {
                ["org.opencontainers.image.description"] = "New image",
            },
            layers:
            [
                new OciDockerLayer(1, BaseDigest, null, 1024),
                new OciDockerLayer(2, CurrentDigest, null, 4096),
            ],
            platforms:
            [
                new OciDockerPlatform(CurrentDigest, "linux", "amd64", null, 5120),
                new OciDockerPlatform(BaseDigest, "linux", "arm64", "v8", 5120),
            ]);

        var comparison = OciDockerImageComparisonBuilder.Compare("1.0.0", previous, "1.1.0", current);

        Assert.True(comparison.HasChanges);
        Assert.Equal("1.0.0", comparison.PreviousVersion);
        Assert.Equal("1.1.0", comparison.CurrentVersion);
        Assert.Contains(comparison.RuntimeChanges, change => change.Label == "Command");
        Assert.Equal(3, comparison.LabelChanges.Count);
        Assert.Equal("changed", Assert.Single(
            comparison.LabelChanges,
            change => change.Key == "org.opencontainers.image.revision").ChangeKind);
        Assert.Single(comparison.AnnotationChanges);
        Assert.Equal(2, comparison.PlatformChanges.Count);
        Assert.Equal(1, comparison.SharedBaseLayerCount);
        Assert.Equal(1, comparison.ReusedLayerCount);
        Assert.Equal(CurrentDigest, Assert.Single(comparison.AddedLayers).Digest);
        Assert.Equal(4096, comparison.AddedLayerBytes);
        Assert.Equal(PreviousDigest, Assert.Single(comparison.RemovedLayers).Digest);
        Assert.False(comparison.LayerOrderChanged);
    }

    [Fact]
    public void Component_ExplainsMetadataOnlyComparisonAndEncodesUntrustedValues()
    {
        using var context = new BunitContext();
        var previous = CreateDetails(
            command: ["safe"],
            labels: new Dictionary<string, string> { ["message"] = "safe" });
        var current = CreateDetails(
            command: ["serve"],
            labels: new Dictionary<string, string> { ["message"] = "<script>alert('x')</script>" });
        var comparison = OciDockerImageComparisonBuilder.Compare("1.0.0", previous, "2.0.0", current);

        var component = context.Render<AvantiPoint.Packages.UI.Components.Oci.OciDockerImageComparison>(parameters => parameters
            .Add(parameter => parameter.Comparison, comparison));

        Assert.Contains("Image changes", component.Markup, StringComparison.Ordinal);
        Assert.Contains("1.0.0 → 2.0.0", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Layers are not downloaded or executed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Runtime configuration", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Label changes", component.Markup, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert('x')&lt;/script&gt;", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert('x')</script>", component.Markup, StringComparison.Ordinal);
    }

    private static OciDockerImageDetails CreateDetails(
        IReadOnlyList<string>? command = null,
        IReadOnlyDictionary<string, string>? labels = null,
        IReadOnlyDictionary<string, string>? annotations = null,
        IReadOnlyList<OciDockerLayer>? layers = null,
        IReadOnlyList<OciDockerPlatform>? platforms = null) => new(
        null,
        "linux",
        "amd64",
        null,
        ["dotnet"],
        command ?? ["App.dll"],
        "/app",
        "10001",
        labels ?? new Dictionary<string, string>(),
        layers ?? [],
        [],
        platforms ?? [],
        annotations ?? new Dictionary<string, string>());
}
