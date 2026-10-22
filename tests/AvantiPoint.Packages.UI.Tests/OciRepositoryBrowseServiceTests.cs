using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.UI.Services;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Registry.Oci;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class OciRepositoryBrowseServiceTests(OpenFeedFactory factory) : IClassFixture<OpenFeedFactory>
{
    [Fact]
    public async Task ArtifactDetail_LoadsHelmConfigAndDocumentsThroughLocalOnlyBlobLookups()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<IContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var repositoryName = $"charts/platform-{suffix}";
        var config = Encoding.UTF8.GetBytes(
            """
            {
              "apiVersion": "v2",
              "name": "platform-stack",
              "version": "3.1.0",
              "description": "A stored Helm chart.",
              "dependencies": [{
                "name": "postgresql",
                "version": "15.x.x",
                "condition": "postgresql.enabled"
              }]
            }
            """);
        var archive = CreateHelmArchive(
            ("platform-stack/Chart.yaml", "apiVersion: v2\nname: platform-stack\nversion: 3.1.0\n"),
            ("platform-stack/README.md", "# Platform Stack\n\nStored chart README."),
            ("platform-stack/values.yaml", "replicaCount: 2\n"),
            ("platform-stack/values.schema.json", "{\"type\":\"object\"}"));
        var configDigest = Digest(config);
        var chartDigest = Digest(archive);
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            mediaType = "application/vnd.oci.image.manifest.v1+json",
            config = new
            {
                mediaType = "application/vnd.cncf.helm.config.v1+json",
                digest = configDigest,
                size = config.Length,
            },
            layers = new[]
            {
                new
                {
                    mediaType = "application/vnd.cncf.helm.chart.content.v1.tar+gzip",
                    digest = chartDigest,
                    size = archive.Length,
                },
            },
        });
        var manifestDigest = Digest(manifest);

        context.OciManifests.Add(new OciManifest
        {
            FeedId = "default",
            Digest = manifestDigest,
            ArtifactKind = OciArtifactKind.Helm,
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Size = manifest.Length,
            CreatedAt = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
        });
        context.OciRepositories.Add(new OciRepository
        {
            FeedId = "default",
            Name = repositoryName,
            Tags =
            [
                new OciTag
                {
                    FeedId = "default",
                    Tag = "3.1.0",
                    ManifestDigest = manifestDigest,
                    Origin = PackageOrigin.Published,
                },
            ],
        });
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var registry = new StoredHelmRegistryService(
            manifestDigest,
            manifest,
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [configDigest] = config,
                [chartDigest] = archive,
            });
        var service = new OciRepositoryBrowseService(
            services.GetRequiredService<IFeedRegistry>(),
            registry,
            context,
            services.GetRequiredService<IHttpContextAccessor>(),
            services.GetRequiredService<IPublicBaseUrlProvider>(),
            services.GetRequiredService<OciFeedOptionsAccessor>());

        var artifact = await service.GetArtifactAsync(
            repositoryName,
            "3.1.0",
            segment: null,
            Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(artifact);
        Assert.Null(artifact.DockerDetails);
        Assert.NotNull(artifact.HelmDetails);
        Assert.Equal("platform-stack", artifact.HelmDetails.Name);
        Assert.Equal("3.1.0", artifact.HelmDetails.Version);
        Assert.Contains("Stored chart README", artifact.HelmDetails.ReadmeHtml, StringComparison.Ordinal);
        Assert.Equal("replicaCount: 2\n", artifact.HelmDetails.ValuesYaml);
        Assert.Equal("postgresql", Assert.Single(artifact.HelmDetails.Dependencies).Name);
        Assert.Equal([configDigest, chartDigest], registry.RequestedDigests);
    }

    [Fact]
    public async Task ArtifactDetail_ComparesHelmChartWithPriorStoredSemanticVersion()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<IContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var repositoryName = $"charts/compare-{suffix}";
        var registryContent = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        (string Digest, byte[] Manifest) CreateVersion(string version, int replicas)
        {
            var config = Encoding.UTF8.GetBytes($$"""
                {
                  "apiVersion": "v2",
                  "name": "platform-stack",
                  "version": "{{version}}"
                }
                """);
            var archive = CreateHelmArchive(
                ("platform-stack/Chart.yaml", $"apiVersion: v2\nname: platform-stack\nversion: {version}\n"),
                ("platform-stack/values.yaml", $"replicaCount: {replicas}\n"));
            var configDigest = Digest(config);
            var archiveDigest = Digest(archive);
            var manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 2,
                mediaType = "application/vnd.oci.image.manifest.v1+json",
                config = new
                {
                    mediaType = "application/vnd.cncf.helm.config.v1+json",
                    digest = configDigest,
                    size = config.Length,
                },
                layers = new[]
                {
                    new
                    {
                        mediaType = "application/vnd.cncf.helm.chart.content.v1.tar+gzip",
                        digest = archiveDigest,
                        size = archive.Length,
                    },
                },
            });
            var manifestDigest = Digest(manifest);
            registryContent[manifestDigest] = manifest;
            registryContent[configDigest] = config;
            registryContent[archiveDigest] = archive;
            return (manifestDigest, manifest);
        }

        var versionOne = CreateVersion("1.0.0", 1);
        var versionTwo = CreateVersion("2.0.0", 2);
        var versionThree = CreateVersion("3.0.0", 3);
        context.OciManifests.AddRange(
            CreateManifest(versionOne.Digest, OciArtifactKind.Helm, DateTime.UtcNow.AddMinutes(-3)),
            CreateManifest(versionTwo.Digest, OciArtifactKind.Helm, DateTime.UtcNow.AddMinutes(-2)),
            CreateManifest(versionThree.Digest, OciArtifactKind.Helm, DateTime.UtcNow.AddMinutes(-1)));
        context.OciRepositories.Add(CreateRepository(
            repositoryName,
            ("1.0.0", versionOne.Digest),
            ("2.0.0", versionTwo.Digest),
            ("3.0.0", versionThree.Digest),
            ("latest", versionThree.Digest)));
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var registry = new StoredVersionedHelmRegistryService(registryContent);
        var service = new OciRepositoryBrowseService(
            services.GetRequiredService<IFeedRegistry>(),
            registry,
            context,
            services.GetRequiredService<IHttpContextAccessor>(),
            services.GetRequiredService<IPublicBaseUrlProvider>(),
            services.GetRequiredService<OciFeedOptionsAccessor>());

        var artifact = await service.GetArtifactAsync(
            repositoryName,
            "2.0.0",
            segment: null,
            Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(artifact);
        Assert.NotNull(artifact.HelmComparison);
        Assert.Equal("1.0.0", artifact.HelmComparison.PreviousVersion);
        Assert.Equal("2.0.0", artifact.HelmComparison.CurrentVersion);
        var values = Assert.Single(
            artifact.HelmComparison.Documents,
            document => document.FileName == "values.yaml");
        Assert.Contains(values.Lines, line => line.Kind == "del" && line.Text == "replicaCount: 1");
        Assert.Contains(values.Lines, line => line.Kind == "add" && line.Text == "replicaCount: 2");
        Assert.Equal([versionTwo.Digest, versionOne.Digest], registry.RequestedManifestReferences);
        Assert.All(registry.BlobRepositoryNames, Assert.Null);
    }

    [Fact]
    public async Task ArtifactDetail_LoadsDockerConfigThroughLocalOnlyBlobLookup()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<IContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var repositoryName = $"docker/config-{suffix}";
        var manifestDigest = $"sha256:{suffix.PadRight(64, 'a')}";
        var configDigest = $"sha256:{suffix.PadRight(64, 'b')}";
        var layerDigest = $"sha256:{suffix.PadRight(64, 'c')}";
        var signatureManifestDigest = $"sha256:{suffix.PadRight(64, 'd')}";
        var signatureReference = $"sha256-{manifestDigest["sha256:".Length..]}.sig";
        var config = Encoding.UTF8.GetBytes("""
            {
              "created": "2026-09-07T10:15:30Z",
              "architecture": "amd64",
              "os": "linux",
              "config": {
                "Entrypoint": ["dotnet"],
                "Cmd": ["App.dll"],
                "Labels": { "org.opencontainers.image.title": "Demo API" }
              },
              "history": [{ "created_by": "ENTRYPOINT [dotnet]" }]
            }
            """);
        var manifest = Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": 2,
              "config": {
                "mediaType": "application/vnd.oci.image.config.v1+json",
                "size": {{config.Length}},
                "digest": "{{configDigest}}"
              },
              "layers": [{
                "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
                "size": 4096,
                "digest": "{{layerDigest}}"
              }]
            }
            """);

        context.OciManifests.Add(new OciManifest
        {
            FeedId = "default",
            Digest = manifestDigest,
            ArtifactKind = OciArtifactKind.Image,
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Size = manifest.Length,
            CreatedAt = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
        });
        context.OciRepositories.Add(new OciRepository
        {
            FeedId = "default",
            Name = repositoryName,
            Tags =
            [
                new OciTag
                {
                    FeedId = "default",
                    Tag = "1.0.0",
                    ManifestDigest = manifestDigest,
                    Origin = PackageOrigin.Published,
                },
                new OciTag
                {
                    FeedId = "default",
                    Tag = signatureReference,
                    ManifestDigest = signatureManifestDigest,
                    Origin = PackageOrigin.Published,
                },
            ],
        });
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var registry = new StoredDockerRegistryService(manifestDigest, manifest, configDigest, config);
        var service = new OciRepositoryBrowseService(
            services.GetRequiredService<IFeedRegistry>(),
            registry,
            context,
            services.GetRequiredService<IHttpContextAccessor>(),
            services.GetRequiredService<IPublicBaseUrlProvider>(),
            services.GetRequiredService<OciFeedOptionsAccessor>());

        var artifact = await service.GetArtifactAsync(
            repositoryName,
            "1.0.0",
            segment: null,
            Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(artifact);
        Assert.Equal("linux", artifact.PlatformOs);
        Assert.Equal("amd64", artifact.PlatformArch);
        Assert.NotNull(artifact.DockerDetails);
        Assert.Equal(["dotnet"], artifact.DockerDetails.Entrypoint);
        Assert.Equal(["App.dll"], artifact.DockerDetails.Command);
        Assert.Equal("Demo API", artifact.DockerDetails.Labels["org.opencontainers.image.title"]);
        Assert.Single(artifact.DockerDetails.Layers);
        Assert.Equal(manifestDigest, artifact.DockerDetails.ManifestDigest);
        Assert.NotNull(artifact.DockerDetails.SupplyChainArtifacts);
        Assert.Equal(3, artifact.DockerDetails.SupplyChainArtifacts.Count);
        var signature = Assert.Single(
            artifact.DockerDetails.SupplyChainArtifacts,
            item => item.Kind == OciDockerSupplyChainArtifactKind.Signature);
        Assert.Equal(OciDockerSupplyChainArtifactKind.Signature, signature.Kind);
        Assert.Equal(signatureReference, signature.Reference);
        Assert.Equal(signatureManifestDigest, signature.ManifestDigest);
        var sbom = Assert.Single(
            artifact.DockerDetails.SupplyChainArtifacts,
            item => item.Kind == OciDockerSupplyChainArtifactKind.Sbom);
        Assert.True(sbom.IsOciReferrer);
        Assert.Equal("application/spdx+json", sbom.ArtifactType);
        var vulnerabilityReport = Assert.Single(
            artifact.DockerDetails.SupplyChainArtifacts,
            item => item.Kind == OciDockerSupplyChainArtifactKind.VulnerabilityReport);
        Assert.True(vulnerabilityReport.IsOciReferrer);
        Assert.NotNull(vulnerabilityReport.VulnerabilitySummary);
        Assert.Equal("Trivy", vulnerabilityReport.VulnerabilitySummary.Format);
        Assert.Equal(1, vulnerabilityReport.VulnerabilitySummary.Critical);
        Assert.Equal(2, vulnerabilityReport.VulnerabilitySummary.High);
        Assert.Equal(
            OciVulnerabilityPolicyStatus.NotConfigured,
            artifact.DockerDetails.VulnerabilityPolicyAssessment?.Status);
        Assert.True(registry.ConfigWasReadLocally);
        Assert.True(registry.ReportWasReadLocally);

        var tags = await service.ListTagsAsync(
            repositoryName,
            segment: null,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.NotNull(tags);
        Assert.Equal("1.0.0", Assert.Single(tags.Tags).Tag);
    }

    [Fact]
    public async Task ArtifactDetail_ComparesDockerImageWithPriorStoredSemanticVersion()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<IContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var repositoryName = $"docker/compare-{suffix}";
        var registryContent = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var baseLayerDigest = $"sha256:{new string('a', 64)}";

        (string Digest, byte[] Manifest) CreateVersion(string version, string command, char layerCharacter)
        {
            var config = Encoding.UTF8.GetBytes($$"""
                {
                  "architecture": "amd64",
                  "os": "linux",
                  "config": {
                    "Entrypoint": ["dotnet"],
                    "Cmd": ["{{command}}"],
                    "Labels": { "org.opencontainers.image.version": "{{version}}" }
                  }
                }
                """);
            var configDigest = Digest(config);
            var changedLayerDigest = $"sha256:{new string(layerCharacter, 64)}";
            var manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 2,
                mediaType = "application/vnd.oci.image.manifest.v1+json",
                config = new
                {
                    mediaType = "application/vnd.oci.image.config.v1+json",
                    digest = configDigest,
                    size = config.Length,
                },
                layers = new[]
                {
                    new
                    {
                        mediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
                        digest = baseLayerDigest,
                        size = 1024,
                    },
                    new
                    {
                        mediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
                        digest = changedLayerDigest,
                        size = 2048,
                    },
                },
            });
            var manifestDigest = Digest(manifest);
            registryContent[manifestDigest] = manifest;
            registryContent[configDigest] = config;
            return (manifestDigest, manifest);
        }

        var versionOne = CreateVersion("1.0.0", "App.dll", 'b');
        var versionTwo = CreateVersion("2.0.0", "Api.dll", 'c');
        context.OciManifests.AddRange(
            CreateManifest(versionOne.Digest, OciArtifactKind.Image, DateTime.UtcNow.AddMinutes(-2)),
            CreateManifest(versionTwo.Digest, OciArtifactKind.Image, DateTime.UtcNow.AddMinutes(-1)));
        context.OciRepositories.Add(CreateRepository(
            repositoryName,
            ("1.0.0", versionOne.Digest),
            ("2.0.0", versionTwo.Digest),
            ("latest", versionTwo.Digest)));
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var registry = new StoredVersionedHelmRegistryService(registryContent);
        var service = new OciRepositoryBrowseService(
            services.GetRequiredService<IFeedRegistry>(),
            registry,
            context,
            services.GetRequiredService<IHttpContextAccessor>(),
            services.GetRequiredService<IPublicBaseUrlProvider>(),
            services.GetRequiredService<OciFeedOptionsAccessor>());

        var artifact = await service.GetArtifactAsync(
            repositoryName,
            "latest",
            segment: null,
            Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(artifact);
        Assert.NotNull(artifact.DockerComparison);
        Assert.Equal("1.0.0", artifact.DockerComparison.PreviousVersion);
        Assert.Equal("2.0.0", artifact.DockerComparison.CurrentVersion);
        Assert.Equal(1, artifact.DockerComparison.SharedBaseLayerCount);
        Assert.Equal(1, artifact.DockerComparison.ReusedLayerCount);
        Assert.Single(artifact.DockerComparison.AddedLayers);
        Assert.Single(artifact.DockerComparison.RemovedLayers);
        Assert.Contains(artifact.DockerComparison.RuntimeChanges, change => change.Label == "Command");
        Assert.Equal([versionTwo.Digest, versionOne.Digest], registry.RequestedManifestReferences);
        Assert.All(registry.BlobRepositoryNames, Assert.Null);
    }

    [Fact]
    public async Task Search_ReportsPlatformFromTheSameManifestAsTheLatestTag()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var amdDigest = $"sha256:{new string('b', 63)}0";
        var armDigest = $"sha256:{new string('c', 63)}0";

        context.OciManifests.AddRange(
            new OciManifest
            {
                FeedId = "default",
                Digest = amdDigest,
                ArtifactKind = OciArtifactKind.Image,
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Size = 512,
                PlatformOs = "linux",
                PlatformArch = "amd64",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            },
            new OciManifest
            {
                FeedId = "default",
                Digest = armDigest,
                ArtifactKind = OciArtifactKind.Image,
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Size = 768,
                PlatformOs = "linux",
                PlatformArch = "arm64",
                CreatedAt = DateTime.UtcNow,
            });
        context.OciRepositories.Add(new OciRepository
        {
            FeedId = "default",
            Name = $"latest/platform-{suffix}",
            Tags =
            [
                new OciTag
                {
                    FeedId = "default",
                    Tag = "1.0.0",
                    ManifestDigest = amdDigest,
                    Origin = PackageOrigin.Published,
                },
                new OciTag
                {
                    FeedId = "default",
                    Tag = "2.0.0",
                    ManifestDigest = armDigest,
                    Origin = PackageOrigin.Published,
                },
            ],
        });
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var service = scope.ServiceProvider.GetRequiredService<IOciRepositoryBrowseService>();
        var result = await service.SearchRepositoriesAsync(
            segment: null,
            query: suffix,
            kind: OciCatalogKind.Docker,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("2.0.0", item.LatestTag);
        Assert.Equal(armDigest, item.LatestDigest);
        Assert.Equal("arm64", item.PlatformArch);
        Assert.Equal(768, item.Size);
    }

    [Fact]
    public async Task CatalogAndVersionLists_UseEcosystemSpecificReferenceOrdering()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var helmRepositoryName = $"charts/order-{suffix}";
        var dockerRepositoryName = $"images/order-{suffix}";
        var helmOneDigest = $"sha256:{suffix.PadRight(64, '1')}";
        var helmTwoDigest = $"sha256:{suffix.PadRight(64, '2')}";
        var helmLatestDigest = $"sha256:{suffix.PadRight(64, '3')}";
        var dockerOneDigest = $"sha256:{suffix.PadRight(64, '4')}";
        var dockerTwoDigest = $"sha256:{suffix.PadRight(64, '5')}";
        var dockerLatestDigest = $"sha256:{suffix.PadRight(64, '6')}";

        context.OciManifests.AddRange(
            CreateManifest(helmOneDigest, OciArtifactKind.Helm, DateTime.UtcNow.AddMinutes(-3)),
            CreateManifest(helmTwoDigest, OciArtifactKind.Helm, DateTime.UtcNow.AddMinutes(-2)),
            CreateManifest(helmLatestDigest, OciArtifactKind.Helm, DateTime.UtcNow),
            CreateManifest(dockerOneDigest, OciArtifactKind.Image, DateTime.UtcNow.AddMinutes(-3)),
            CreateManifest(dockerTwoDigest, OciArtifactKind.Image, DateTime.UtcNow.AddMinutes(-2)),
            CreateManifest(dockerLatestDigest, OciArtifactKind.Image, DateTime.UtcNow));
        context.OciRepositories.AddRange(
            CreateRepository(
                helmRepositoryName,
                ("1.0.0", helmOneDigest),
                ("2.0.0", helmTwoDigest),
                ("latest", helmLatestDigest)),
            CreateRepository(
                dockerRepositoryName,
                ("1.0.0", dockerOneDigest),
                ("2.0.0", dockerTwoDigest),
                ("latest", dockerLatestDigest)));
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var service = scope.ServiceProvider.GetRequiredService<IOciRepositoryBrowseService>();
        var catalog = await service.SearchRepositoriesAsync(
            segment: null,
            query: suffix,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);
        var helmVersions = await service.ListTagsAsync(
            helmRepositoryName,
            segment: null,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);
        var dockerTags = await service.ListTagsAsync(
            dockerRepositoryName,
            segment: null,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("2.0.0", Assert.Single(catalog.Items, item => item.Name == helmRepositoryName).LatestTag);
        Assert.Equal("latest", Assert.Single(catalog.Items, item => item.Name == dockerRepositoryName).LatestTag);
        Assert.NotNull(helmVersions);
        Assert.NotNull(dockerTags);
        Assert.Equal(["2.0.0", "1.0.0", "latest"], helmVersions.Tags.Select(tag => tag.Tag));
        Assert.Equal(["latest", "2.0.0", "1.0.0"], dockerTags.Tags.Select(tag => tag.Tag));
    }

    [Fact]
    public async Task Search_FiltersBeforePaging_AndExcludesNonDiscoverableRepositories()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        context.OciManifests.Add(new OciManifest
        {
            FeedId = "default",
            Digest = digest,
            ArtifactKind = OciArtifactKind.Image,
            MediaType = "application/vnd.oci.image.manifest.v1+json",
            Size = 512,
            PlatformOs = "linux",
            PlatformArch = "amd64",
            CreatedAt = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
        });

        for (var index = 0; index < 225; index++)
        {
            AddRepository(context, $"ordinary/image-{index:D3}", digest, PackageOrigin.Published);
        }

        AddRepository(context, "zzzz/needle-image", digest, PackageOrigin.Published);
        AddRepository(context, "zzzz/needle-cached", digest, PackageOrigin.Cached);
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var service = scope.ServiceProvider.GetRequiredService<IOciRepositoryBrowseService>();
        var result = await service.SearchRepositoriesAsync(
            segment: null,
            query: "needle",
            kind: OciCatalogKind.Docker,
            skip: 0,
            take: 24,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("zzzz/needle-image", item.Name);
        Assert.Equal(digest, item.LatestDigest);
        Assert.Equal(1, result.TotalCount);
    }

    private static void AddRepository(
        IContext context,
        string name,
        string digest,
        PackageOrigin origin)
    {
        context.OciRepositories.Add(new OciRepository
        {
            FeedId = "default",
            Name = name,
            Tags =
            [
                new OciTag
                {
                    FeedId = "default",
                    Tag = "1.0.0",
                    ManifestDigest = digest,
                    Origin = origin,
                },
            ],
        });
    }

    private static OciManifest CreateManifest(
        string digest,
        OciArtifactKind artifactKind,
        DateTime createdAt) => new()
    {
        FeedId = "default",
        Digest = digest,
        ArtifactKind = artifactKind,
        MediaType = "application/vnd.oci.image.manifest.v1+json",
        Size = 512,
        CreatedAt = createdAt,
    };

    private static OciRepository CreateRepository(
        string name,
        params (string Tag, string Digest)[] tags) => new()
    {
        FeedId = "default",
        Name = name,
        Tags = tags.Select(tag => new OciTag
        {
            FeedId = "default",
            Tag = tag.Tag,
            ManifestDigest = tag.Digest,
            Origin = PackageOrigin.Published,
        }).ToList(),
    };

    private static byte[] CreateHelmArchive(params (string Path, string Content)[] files)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            foreach (var (path, content) in files)
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(bytes, writable: false),
                });
            }
        }

        return output.ToArray();
    }

    private static string Digest(byte[] content) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}";

    private sealed class StoredHelmRegistryService(
        string manifestDigest,
        byte[] manifest,
        IReadOnlyDictionary<string, byte[]> blobs) : IOciRegistryService
    {
        public List<string> RequestedDigests { get; } = [];

        public Task<OciManifestResult?> GetManifestAsync(
            SurfaceContext surface,
            string repositoryName,
            string reference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OciManifestResult?>(new(
                manifestDigest,
                "application/vnd.oci.image.manifest.v1+json",
                manifest));

        public Task<OciBlobResult?> GetBlobAsync(
            SurfaceContext surface,
            string digest,
            string? repositoryName = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Null(repositoryName);
            RequestedDigests.Add(digest);
            return Task.FromResult<OciBlobResult?>(blobs.TryGetValue(digest, out var content)
                ? new OciBlobResult(digest, new MemoryStream(content, writable: false), content.Length)
                : null);
        }

        public Task<OciPutManifestResult> PutManifestAsync(SurfaceContext surface, string repositoryName, string reference, string mediaType, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciBlobExistsResult> BlobExistsAsync(SurfaceContext surface, string digest, string? repositoryName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciStartUploadResult> StartUploadAsync(SurfaceContext surface, string repositoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciPatchUploadResult> PatchUploadAsync(SurfaceContext surface, string repositoryName, string uploadId, Stream content, long? start, long? end, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciCompleteUploadResult> CompleteUploadAsync(SurfaceContext surface, string repositoryName, string uploadId, string digest, Stream? content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciTagListResult?> ListTagsAsync(SurfaceContext surface, string repositoryName, int? max, string? last, CancellationToken cancellationToken = default) => Task.FromResult<OciTagListResult?>(null);
        public Task<OciCatalogResult> ListCatalogAsync(SurfaceContext surface, int? max, string? last, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StoredVersionedHelmRegistryService(
        IReadOnlyDictionary<string, byte[]> content) : IOciRegistryService
    {
        public List<string> RequestedManifestReferences { get; } = [];

        public List<string?> BlobRepositoryNames { get; } = [];

        public Task<OciManifestResult?> GetManifestAsync(
            SurfaceContext surface,
            string repositoryName,
            string reference,
            CancellationToken cancellationToken = default)
        {
            RequestedManifestReferences.Add(reference);
            return Task.FromResult<OciManifestResult?>(content.TryGetValue(reference, out var manifest)
                ? new OciManifestResult(reference, "application/vnd.oci.image.manifest.v1+json", manifest)
                : null);
        }

        public Task<OciBlobResult?> GetBlobAsync(
            SurfaceContext surface,
            string digest,
            string? repositoryName = null,
            CancellationToken cancellationToken = default)
        {
            BlobRepositoryNames.Add(repositoryName);
            return Task.FromResult<OciBlobResult?>(content.TryGetValue(digest, out var blob)
                ? new OciBlobResult(digest, new MemoryStream(blob, writable: false), blob.Length)
                : null);
        }

        public Task<OciPutManifestResult> PutManifestAsync(SurfaceContext surface, string repositoryName, string reference, string mediaType, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciBlobExistsResult> BlobExistsAsync(SurfaceContext surface, string digest, string? repositoryName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciStartUploadResult> StartUploadAsync(SurfaceContext surface, string repositoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciPatchUploadResult> PatchUploadAsync(SurfaceContext surface, string repositoryName, string uploadId, Stream content, long? start, long? end, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciCompleteUploadResult> CompleteUploadAsync(SurfaceContext surface, string repositoryName, string uploadId, string digest, Stream? content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciTagListResult?> ListTagsAsync(SurfaceContext surface, string repositoryName, int? max, string? last, CancellationToken cancellationToken = default) => Task.FromResult<OciTagListResult?>(null);
        public Task<OciCatalogResult> ListCatalogAsync(SurfaceContext surface, int? max, string? last, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StoredDockerRegistryService(
        string manifestDigest,
        byte[] manifest,
        string configDigest,
        byte[] config) : IOciRegistryService
    {
        private const string ReportManifestDigest = "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
        private const string ReportLayerDigest = "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        private readonly byte[] _report = Encoding.UTF8.GetBytes("""
            {
              "SchemaVersion": 2,
              "Results": [{
                "Target": "demo",
                "Vulnerabilities": [
                  { "VulnerabilityID": "CVE-1", "Severity": "CRITICAL" },
                  { "VulnerabilityID": "CVE-2", "Severity": "HIGH" },
                  { "VulnerabilityID": "CVE-3", "Severity": "HIGH" }
                ]
              }]
            }
            """);

        public bool ConfigWasReadLocally { get; private set; }

        public bool ReportWasReadLocally { get; private set; }

        public Task<OciManifestResult?> GetManifestAsync(
            SurfaceContext surface,
            string repositoryName,
            string reference,
            CancellationToken cancellationToken = default)
        {
            if (reference == ReportManifestDigest)
            {
                var reportManifest = Encoding.UTF8.GetBytes($$"""
                    {
                      "schemaVersion": 2,
                      "artifactType": "application/vnd.aquasec.trivy.report+json",
                      "config": {
                        "mediaType": "application/vnd.unknown.config.v1+json",
                        "digest": "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                        "size": 2
                      },
                      "layers": [{
                        "mediaType": "application/vnd.aquasec.trivy.report.layer.v1+json",
                        "digest": "{{ReportLayerDigest}}",
                        "size": {{_report.Length}}
                      }]
                    }
                    """);
                return Task.FromResult<OciManifestResult?>(new(
                    ReportManifestDigest,
                    "application/vnd.oci.image.manifest.v1+json",
                    reportManifest));
            }

            return Task.FromResult<OciManifestResult?>(new(
                manifestDigest,
                "application/vnd.oci.image.manifest.v1+json",
                manifest));
        }

        public Task<OciBlobResult?> GetBlobAsync(
            SurfaceContext surface,
            string digest,
            string? repositoryName = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Null(repositoryName);
            if (digest == configDigest)
            {
                ConfigWasReadLocally = true;
                return Task.FromResult<OciBlobResult?>(new(
                    configDigest,
                    new MemoryStream(config, writable: false),
                    config.Length));
            }

            Assert.Equal(ReportLayerDigest, digest);
            ReportWasReadLocally = true;
            return Task.FromResult<OciBlobResult?>(new(
                ReportLayerDigest,
                new MemoryStream(_report, writable: false),
                _report.Length));
        }

        public Task<OciReferrersResult> ListReferrersAsync(
            SurfaceContext surface,
            string repositoryName,
            string subjectDigest,
            string? artifactType = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new OciReferrersResult(
            [
                new OciReferrerDescriptor(
                    "application/vnd.oci.image.manifest.v1+json",
                    "sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                    256,
                    "application/spdx+json",
                    new Dictionary<string, string>()),
                new OciReferrerDescriptor(
                    "application/vnd.oci.image.manifest.v1+json",
                    ReportManifestDigest,
                    512,
                    "application/vnd.aquasec.trivy.report+json",
                    new Dictionary<string, string>()),
            ]));

        public Task<OciPutManifestResult> PutManifestAsync(SurfaceContext surface, string repositoryName, string reference, string mediaType, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciBlobExistsResult> BlobExistsAsync(SurfaceContext surface, string digest, string? repositoryName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciStartUploadResult> StartUploadAsync(SurfaceContext surface, string repositoryName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciPatchUploadResult> PatchUploadAsync(SurfaceContext surface, string repositoryName, string uploadId, Stream content, long? start, long? end, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciCompleteUploadResult> CompleteUploadAsync(SurfaceContext surface, string repositoryName, string uploadId, string digest, Stream? content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OciTagListResult?> ListTagsAsync(SurfaceContext surface, string repositoryName, int? max, string? last, CancellationToken cancellationToken = default) => Task.FromResult<OciTagListResult?>(null);
        public Task<OciCatalogResult> ListCatalogAsync(SurfaceContext surface, int? max, string? last, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
