using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Oci;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Services;

/// <summary>
/// Adds small, valid artifacts to development hosts so ecosystem-native experiences are
/// visible without affecting production defaults or contacting upstream registries.
/// </summary>
public sealed partial class DevelopmentDemoDataSeeder(
    IFeedRegistry feedRegistry,
    IContext context,
    IServiceProvider services,
    ILogger<DevelopmentDemoDataSeeder> logger)
{
    private IOciRegistryService registryService => services.GetRequiredService<IOciRegistryService>();
    private const string HelmSegment = "helm";
    private const string DockerSegment = "docker";
    private const string HelmRepositoryName = "charts/platform-stack";
    private const string DockerDemoRepositoryName = "demo/unified-feed";
    private static readonly DemoChartVersion[] ChartVersions =
    [
        new("0.2.0", "3.9.0", 1, "10Gi"),
        new("0.3.0", "4.0.0", 2, "20Gi"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedNuGetAsync(cancellationToken);
        await SeedNpmAsync(cancellationToken);
        var registration = feedRegistry.TryGetOciSurfaceBySegment(HelmSegment);
        if (registration is not null)
        {
            var surface = CreateSurfaceContext(registration);
            foreach (var chartVersion in ChartVersions)
            {
                var versionExists = await context.OciTags
                    .AsNoTracking()
                    .AnyAsync(
                        tag => tag.FeedId == feedRegistry.Feed.FeedId
                               && tag.OciSegment == registration.OciSegment
                               && tag.Repository.Name == HelmRepositoryName
                               && tag.Tag == chartVersion.Version,
                        cancellationToken);
                if (versionExists)
                {
                    continue;
                }

                var config = CreateChartConfig(chartVersion);
                var chartArchive = CreateChartArchive(chartVersion);
                var configDigest = await UploadBlobAsync(surface, HelmRepositoryName, config, cancellationToken);
                var chartDigest = await UploadBlobAsync(surface, HelmRepositoryName, chartArchive, cancellationToken);
                var manifest = CreateManifest(configDigest, config.Length, chartDigest, chartArchive.Length);

                await using (var versionContent = new MemoryStream(manifest, writable: false))
                {
                    await registryService.PutManifestAsync(
                        surface,
                        HelmRepositoryName,
                        chartVersion.Version,
                        "application/vnd.oci.image.manifest.v1+json",
                        versionContent,
                        cancellationToken);
                }

                logger.LogInformation(
                    "Seeded development Helm chart {Repository}:{Version}",
                    HelmRepositoryName,
                    chartVersion.Version);
            }
        }

        var dockerRegistration = feedRegistry.TryGetOciSurfaceBySegment(DockerSegment);
        if (dockerRegistration is not null)
        {
            await SeedDockerImagesAsync(CreateSurfaceContext(dockerRegistration), cancellationToken);
            await SeedDockerSbomAsync(CreateSurfaceContext(dockerRegistration), cancellationToken);
        }
    }

    private async Task<string> UploadBlobAsync(
        SurfaceContext surface,
        string repositoryName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var digest = $"sha256:{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}";
        var upload = await registryService.StartUploadAsync(surface, repositoryName, cancellationToken);
        await using var stream = new MemoryStream(content, writable: false);
        await registryService.CompleteUploadAsync(
            surface,
            repositoryName,
            upload.UploadId,
            digest,
            stream,
            cancellationToken);
        return digest;
    }

    private SurfaceContext CreateSurfaceContext(SurfaceRegistration registration) => new(
        feedRegistry.Feed.FeedId,
        FeedProtocol.Oci,
        registration.SurfaceId,
        registration.OciSegment,
        registration.RoutePrefix,
        new Uri($"http://localhost{registration.RoutePrefix.TrimEnd('/')}/"));

    private async Task SeedDockerSbomAsync(
        SurfaceContext surface,
        CancellationToken cancellationToken)
    {
        var image = await (
                from tag in context.OciTags.AsNoTracking()
                join manifest in context.OciManifests.AsNoTracking()
                    on new { tag.FeedId, tag.OciSegment, Digest = tag.ManifestDigest }
                    equals new { manifest.FeedId, manifest.OciSegment, Digest = manifest.Digest }
                where tag.FeedId == feedRegistry.Feed.FeedId
                      && tag.OciSegment == surface.OciSegment
                      && tag.Repository.Name == DockerDemoRepositoryName
                      && tag.Tag == "1.1.0"
                select new { manifest.Digest, manifest.MediaType, manifest.Size })
            .FirstOrDefaultAsync(cancellationToken);
        if (image is null)
        {
            return;
        }

        var existing = await registryService.ListReferrersAsync(
            surface,
            DockerDemoRepositoryName,
            image.Digest,
            "application/spdx+json",
            cancellationToken);
        if (existing.Manifests.Count > 0)
        {
            return;
        }

        var emptyConfig = Encoding.UTF8.GetBytes("{}");
        var sbom = JsonSerializer.SerializeToUtf8Bytes(new
        {
            spdxVersion = "SPDX-2.3",
            dataLicense = "CC0-1.0",
            SPDXID = "SPDXRef-DOCUMENT",
            name = "demo-unified-feed-1.1.0",
            documentNamespace = "https://avantipoint.com/spdx/demo-unified-feed-1.1.0",
            creationInfo = new
            {
                created = "2026-09-08T12:00:00Z",
                creators = new[] { "Tool: AvantiPoint development data seeder" },
            },
            packages = Array.Empty<object>(),
        });
        var configDigest = await UploadBlobAsync(
            surface,
            DockerDemoRepositoryName,
            emptyConfig,
            cancellationToken);
        var sbomDigest = await UploadBlobAsync(
            surface,
            DockerDemoRepositoryName,
            sbom,
            cancellationToken);
        var artifactManifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            mediaType = "application/vnd.oci.image.manifest.v1+json",
            artifactType = "application/spdx+json",
            config = new
            {
                mediaType = "application/vnd.oci.empty.v1+json",
                digest = configDigest,
                size = emptyConfig.Length,
            },
            layers = new[]
            {
                new
                {
                    mediaType = "application/spdx+json",
                    digest = sbomDigest,
                    size = sbom.Length,
                },
            },
            subject = new
            {
                mediaType = image.MediaType,
                digest = image.Digest,
                size = image.Size,
            },
            annotations = new Dictionary<string, string>
            {
                ["org.opencontainers.image.title"] = "SPDX software bill of materials",
                ["org.opencontainers.image.created"] = "2026-09-08T12:00:00Z",
            },
        });
        var artifactDigest = $"sha256:{Convert.ToHexString(SHA256.HashData(artifactManifest)).ToLowerInvariant()}";
        await using var content = new MemoryStream(artifactManifest, writable: false);
        await registryService.PutManifestAsync(
            surface,
            DockerDemoRepositoryName,
            artifactDigest,
            "application/vnd.oci.image.manifest.v1+json",
            content,
            cancellationToken);
        logger.LogInformation(
            "Attached development SPDX SBOM {ArtifactDigest} to {Repository}@{Digest}",
            artifactDigest,
            DockerDemoRepositoryName,
            image.Digest);
    }

    private static byte[] CreateChartConfig(DemoChartVersion chartVersion) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        apiVersion = "v2",
        name = "platform-stack",
        version = chartVersion.Version,
        appVersion = chartVersion.AppVersion,
        kubeVersion = ">=1.29.0-0",
        description = "A deploy-anywhere package platform with NuGet, npm, Docker, and Helm feeds.",
        type = "application",
        home = "https://github.com/AvantiPoint/avantipoint.packages",
        sources = new[] { "https://github.com/AvantiPoint/avantipoint.packages" },
        keywords = new[] { "packages", "nuget", "npm", "docker", "helm", "oci" },
        maintainers = new[]
        {
            new { name = "AvantiPoint", url = "https://avantipoint.com" },
        },
        dependencies = new[]
        {
            new
            {
                name = "postgresql",
                version = "15.5.0",
                repository = "https://charts.bitnami.com/bitnami",
                condition = "postgresql.enabled",
                tags = new[] { "database" },
            },
        },
    });

    private static byte[] CreateChartArchive(DemoChartVersion chartVersion)
    {
        var files = new Dictionary<string, string>
        {
            ["platform-stack/Chart.yaml"] = $$"""
                apiVersion: v2
                name: platform-stack
                description: A deploy-anywhere package platform with NuGet, npm, Docker, and Helm feeds.
                type: application
                version: {{chartVersion.Version}}
                appVersion: "{{chartVersion.AppVersion}}"
                kubeVersion: ">=1.29.0-0"
                home: https://github.com/AvantiPoint/avantipoint.packages
                sources:
                  - https://github.com/AvantiPoint/avantipoint.packages
                maintainers:
                  - name: AvantiPoint
                    url: https://avantipoint.com
                dependencies:
                  - name: postgresql
                    version: 15.5.0
                    repository: https://charts.bitnami.com/bitnami
                    condition: postgresql.enabled
                    tags:
                      - database
                """,
            ["platform-stack/README.md"] = $$"""
                # Platform Stack

                Deploy a single package platform with ecosystem-native NuGet, npm, Docker, and Helm experiences.

                ## Quick start

                ```shell
                helm upgrade --install packages oci://localhost:8080/helm/charts/platform-stack --version {{chartVersion.Version}} --plain-http
                ```

                The bundled PostgreSQL dependency is disabled by default so the chart can use an externally managed database.
                """,
            ["platform-stack/values.yaml"] = $$"""
                replicaCount: {{chartVersion.ReplicaCount}}

                image:
                  repository: ghcr.io/avantipoint/packages
                  tag: latest

                persistence:
                  enabled: true
                  size: {{chartVersion.PersistenceSize}}

                postgresql:
                  enabled: false
                """,
            ["platform-stack/values.schema.json"] = """
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "type": "object",
                  "properties": {
                    "replicaCount": { "type": "integer", "minimum": 1 },
                    "persistence": {
                      "type": "object",
                      "properties": {
                        "enabled": { "type": "boolean" },
                        "size": { "type": "string" }
                      }
                    },
                    "postgresql": {
                      "type": "object",
                      "properties": { "enabled": { "type": "boolean" } }
                    }
                  }
                }
                """,
            ["platform-stack/templates/deployment.yaml"] = """
                apiVersion: apps/v1
                kind: Deployment
                metadata:
                  name: {{ .Release.Name }}-packages
                spec:
                  replicas: {{ .Values.replicaCount }}
                  selector:
                    matchLabels:
                      app.kubernetes.io/name: packages
                  template:
                    metadata:
                      labels:
                        app.kubernetes.io/name: packages
                    spec:
                      containers:
                        - name: packages
                          image: "{{ .Values.image.repository }}:{{ .Values.image.tag }}"
                          ports:
                            - name: http
                              containerPort: 8080
                """,
            ["platform-stack/charts/postgresql/Chart.yaml"] = """
                apiVersion: v2
                name: postgresql
                version: 15.5.0
                type: application
                """,
            ["platform-stack/charts/postgresql/values.yaml"] = "{}\n",
        };

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            foreach (var (path, value) in files.OrderBy(file => file.Key, StringComparer.Ordinal))
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(bytes, writable: false),
                    ModificationTime = DateTimeOffset.UnixEpoch,
                };
                writer.WriteEntry(entry);
            }
        }

        return output.ToArray();
    }

    private sealed record DemoChartVersion(
        string Version,
        string AppVersion,
        int ReplicaCount,
        string PersistenceSize);

    private static byte[] CreateManifest(
        string configDigest,
        int configSize,
        string chartDigest,
        int chartSize) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = 2,
        mediaType = "application/vnd.oci.image.manifest.v1+json",
        config = new
        {
            mediaType = "application/vnd.cncf.helm.config.v1+json",
            digest = configDigest,
            size = configSize,
        },
        layers = new[]
        {
            new
            {
                mediaType = "application/vnd.cncf.helm.chart.content.v1.tar+gzip",
                digest = chartDigest,
                size = chartSize,
            },
        },
        annotations = new Dictionary<string, string>
        {
            ["org.opencontainers.image.title"] = "platform-stack",
            ["org.opencontainers.image.description"] = "AvantiPoint package platform demo chart",
        },
    });
}
