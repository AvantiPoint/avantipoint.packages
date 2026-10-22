using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Registry.Oci;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class HelmChartArtifactReaderTests
{
    [Theory]
    [InlineData("application/vnd.cncf.helm.chart.content.v1.tar+gzip")]
    [InlineData("application/tar+gzip")]
    public async Task ReadAsync_ExtractsOfficialMetadataAndRootChartDocuments(string layerMediaType)
    {
        var config = Encoding.UTF8.GetBytes(
            """
            {
              "apiVersion": "v2",
              "name": "platform-stack",
              "version": "1.4.0",
              "appVersion": "9.2.1",
              "kubeVersion": ">=1.29.0-0",
              "description": "Deploys the unified package platform.",
              "type": "application",
              "home": "https://example.test/charts/platform-stack",
              "icon": "javascript:alert('x')",
              "sources": ["https://example.test/source", "javascript:alert('x')"],
              "keywords": ["packages", "registry"],
              "maintainers": [{"name":"Platform Team","email":"team@example.test"}],
              "dependencies": [{
                "name": "postgresql",
                "version": "15.x.x",
                "repository": "https://charts.example.test",
                "condition": "postgresql.enabled",
                "tags": ["database"]
              }]
            }
            """);
        var archive = CreateChartArchive(
            ("platform-stack/Chart.yaml", "apiVersion: v2\nname: platform-stack\nversion: 1.4.0\n"),
            ("platform-stack/README.md", "# Platform Stack\n[unsafe](javascript:alert('x'))\n<script>alert('x')</script>"),
            ("platform-stack/values.yaml", "replicaCount: 2\nmessage: <script>alert('x')</script>\n"),
            ("platform-stack/values.schema.json", "{\"type\":\"object\",\"properties\":{\"replicaCount\":{\"type\":\"integer\"}}}"),
            ("platform-stack/Chart.lock", "dependencies: []\n"),
            ("platform-stack/charts/subchart/Chart.yaml", "name: must-not-win\n"));
        var configDigest = Digest(config);
        var chartDigest = Digest(archive);
        var provenanceDigest = Digest(Encoding.UTF8.GetBytes("signed"));
        var unknownDigest = Digest(Encoding.UTF8.GetBytes("unknown"));
        var manifest = CreateManifest(
            configDigest,
            config.Length,
            chartDigest,
            archive.Length,
            layerMediaType,
            provenanceDigest,
            unknownDigest);
        var blobs = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            [configDigest] = config,
            [chartDigest] = archive,
        };
        var requested = new List<string>();

        var details = await HelmChartArtifactReader.ReadAsync(
            manifest,
            (digest, _) =>
            {
                requested.Add(digest);
                return Task.FromResult<OciBlobResult?>(blobs.TryGetValue(digest, out var bytes)
                    ? new OciBlobResult(digest, new MemoryStream(bytes, writable: false), bytes.Length)
                    : null);
            },
            Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([configDigest, chartDigest], requested);
        Assert.Equal("platform-stack", details.Name);
        Assert.Equal("1.4.0", details.Version);
        Assert.Equal("9.2.1", details.AppVersion);
        Assert.Equal(">=1.29.0-0", details.KubeVersion);
        Assert.Equal("application", details.Type);
        Assert.Equal(["packages", "registry"], details.Keywords);
        Assert.Equal("Platform Team", Assert.Single(details.Maintainers).Name);
        var dependency = Assert.Single(details.Dependencies);
        Assert.Equal("postgresql", dependency.Name);
        Assert.Equal("postgresql.enabled", dependency.Condition);
        Assert.Equal("replicaCount: 2\nmessage: <script>alert('x')</script>\n", details.ValuesYaml);
        Assert.Contains("\n  \"properties\"", details.ValuesSchemaJson, StringComparison.Ordinal);
        Assert.Contains("name: platform-stack", details.ChartYaml, StringComparison.Ordinal);
        Assert.Equal("dependencies: []\n", details.ChartLockYaml);
        Assert.True(details.HasProvenance);
        Assert.Null(details.UnavailableReason);
        Assert.Contains("<h1", details.ReadmeHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", details.ReadmeHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", details.ReadmeHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_InvalidArchive_PreservesConfigMetadataAndReportsLimitedPreview()
    {
        var config = Encoding.UTF8.GetBytes("{\"name\":\"valid-config\",\"version\":\"2.0.0\"}");
        var archive = Encoding.UTF8.GetBytes("this is not a gzip archive");
        var configDigest = Digest(config);
        var chartDigest = Digest(archive);
        var manifest = CreateManifest(
            configDigest,
            config.Length,
            chartDigest,
            archive.Length,
            "application/vnd.cncf.helm.chart.content.v1.tar+gzip");

        var details = await HelmChartArtifactReader.ReadAsync(
            manifest,
            (digest, _) => Task.FromResult<OciBlobResult?>(digest == configDigest
                ? new OciBlobResult(digest, new MemoryStream(config), config.Length)
                : new OciBlobResult(digest, new MemoryStream(archive), archive.Length)),
            Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("valid-config", details.Name);
        Assert.Equal("2.0.0", details.Version);
        Assert.Contains("archive is invalid", details.UnavailableReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(details.ChartYaml);
    }

    [Fact]
    public async Task ReadAsync_MultipleChartRoots_AreRejectedInsteadOfChoosingArbitrarily()
    {
        var config = Encoding.UTF8.GetBytes("{\"name\":\"ambiguous\",\"version\":\"1.0.0\"}");
        var archive = CreateChartArchive(
            ("first/Chart.yaml", "name: first\n"),
            ("second/Chart.yaml", "name: second\n"));
        var configDigest = Digest(config);
        var chartDigest = Digest(archive);
        var manifest = CreateManifest(
            configDigest,
            config.Length,
            chartDigest,
            archive.Length,
            "application/vnd.cncf.helm.chart.content.v1.tar+gzip");

        var details = await HelmChartArtifactReader.ReadAsync(
            manifest,
            (digest, _) => Task.FromResult<OciBlobResult?>(digest == configDigest
                ? new OciBlobResult(digest, new MemoryStream(config), config.Length)
                : new OciBlobResult(digest, new MemoryStream(archive), archive.Length)),
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("multiple chart roots", details.UnavailableReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(details.ChartYaml);
    }

    [Fact]
    public async Task ReadAsync_DuplicateRootDocument_IsRejectedInsteadOfUsingFirstEntry()
    {
        var config = Encoding.UTF8.GetBytes("{\"name\":\"duplicate\",\"version\":\"1.0.0\"}");
        var archive = CreateChartArchive(
            ("duplicate/Chart.yaml", "name: first\n"),
            ("duplicate/Chart.yaml", "name: replacement\n"));
        var configDigest = Digest(config);
        var chartDigest = Digest(archive);
        var manifest = CreateManifest(
            configDigest,
            config.Length,
            chartDigest,
            archive.Length,
            "application/vnd.cncf.helm.chart.content.v1.tar+gzip");

        var details = await HelmChartArtifactReader.ReadAsync(
            manifest,
            (digest, _) => Task.FromResult<OciBlobResult?>(digest == configDigest
                ? new OciBlobResult(digest, new MemoryStream(config), config.Length)
                : new OciBlobResult(digest, new MemoryStream(archive), archive.Length)),
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("duplicate root document", details.UnavailableReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(details.ChartYaml);
    }

    [Fact]
    public async Task ReadAsync_DoesNotSwallowCancellation()
    {
        var config = Encoding.UTF8.GetBytes("{}");
        var configDigest = Digest(config);
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            config = new
            {
                mediaType = "application/vnd.cncf.helm.config.v1+json",
                digest = configDigest,
                size = config.Length,
            },
            layers = Array.Empty<object>(),
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HelmChartArtifactReader.ReadAsync(
                manifest,
                (_, token) => Task.FromCanceled<OciBlobResult?>(token),
                cancellation.Token));
    }

    private static byte[] CreateChartArchive(params (string Path, string Content)[] files)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            foreach (var (path, content) in files)
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(bytes, writable: false),
                };
                writer.WriteEntry(entry);
            }
        }

        return output.ToArray();
    }

    private static byte[] CreateManifest(
        string configDigest,
        int configSize,
        string chartDigest,
        int chartSize,
        string chartMediaType,
        string? provenanceDigest = null,
        string? unknownDigest = null)
    {
        var layers = new List<object>();
        if (unknownDigest is not null)
        {
            layers.Add(new
            {
                mediaType = "application/vnd.example.unknown",
                digest = unknownDigest,
                size = 7,
            });
        }

        if (provenanceDigest is not null)
        {
            layers.Add(new
            {
                mediaType = "application/vnd.cncf.helm.chart.provenance.v1.prov",
                digest = provenanceDigest,
                size = 6,
            });
        }

        layers.Add(new
        {
            mediaType = chartMediaType,
            digest = chartDigest,
            size = chartSize,
        });

        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            config = new
            {
                mediaType = "application/vnd.cncf.helm.config.v1+json",
                digest = configDigest,
                size = configSize,
            },
            layers,
        });
    }

    private static string Digest(byte[] content) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}";
}
