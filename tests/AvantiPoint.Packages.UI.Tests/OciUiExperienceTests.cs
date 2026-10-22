using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.UI.Components.Oci;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class OciUiExperienceTests
{
    private const string Digest = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void HelmDigestCommands_KeepImmutableReference_AndDoNotOfferImageTools()
    {
        var commands = OciCommandBuilder.BuildArtifactCommands(
            "packages.example.test/helm",
            "platform/api",
            Digest,
            OciArtifactKind.Helm).ToList();

        Assert.Contains(commands, command => command == $"helm pull oci://packages.example.test/helm/platform/api@{Digest}");
        Assert.Contains(commands, command => command == $"helm upgrade --install api oci://packages.example.test/helm/platform/api@{Digest}");
        Assert.Contains(commands, command => command == $"oras pull packages.example.test/helm/platform/api@{Digest}");
        Assert.DoesNotContain(commands, command => command.StartsWith("docker ", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.StartsWith("crane ", StringComparison.Ordinal));
    }

    [Fact]
    public void DockerCommands_IncludeRequiredCraneOutput()
    {
        var commands = OciCommandBuilder.BuildArtifactCommands(
            "packages.example.test/docker",
            "platform/api",
            "2.1.0",
            OciArtifactKind.Image).ToList();

        Assert.Contains("docker pull packages.example.test/docker/platform/api:2.1.0", commands);
        Assert.Contains("oras pull packages.example.test/docker/platform/api:2.1.0", commands);
        Assert.Contains("crane pull packages.example.test/docker/platform/api:2.1.0 image.tar", commands);
        Assert.DoesNotContain(commands, command => command.StartsWith("helm ", StringComparison.Ordinal));
    }

    [Fact]
    public void SupplyChainCommands_AreDigestPinnedAndRespectLocalHttpRegistries()
    {
        var signature = OciCommandBuilder.BuildCosignVerify(
            "localhost:8080/docker",
            "platform/api",
            Digest,
            allowHttpRegistry: true);
        var attestation = OciCommandBuilder.BuildCosignVerifyAttestation(
            "localhost:8080/docker",
            "platform/api",
            Digest,
            allowHttpRegistry: true);
        var helm = OciCommandBuilder.BuildHelmVerifiedPull(
            "localhost:8080/helm",
            "charts/platform-stack",
            Digest,
            plainHttp: true);

        Assert.Equal(
            $"cosign verify --key /path/to/trusted-cosign.pub --allow-http-registry localhost:8080/docker/platform/api@{Digest}",
            signature);
        Assert.Equal(
            $"cosign verify-attestation --key /path/to/trusted-cosign.pub --allow-http-registry localhost:8080/docker/platform/api@{Digest}",
            attestation);
        Assert.Equal(
            $"helm pull oci://localhost:8080/helm/charts/platform-stack@{Digest} --verify --plain-http",
            helm);
    }

    [Fact]
    public void ArtifactCommands_AddEachClientsHttpRegistryOptionOnlyWhenRequired()
    {
        var dockerCommands = OciCommandBuilder.BuildArtifactCommands(
            "localhost:8080/docker",
            "platform/api",
            "2.1.0",
            OciArtifactKind.Image,
            plainHttp: true).ToArray();
        var helmCommands = OciCommandBuilder.BuildArtifactCommands(
            "localhost:8080/helm",
            "charts/platform-stack",
            "1.4.0",
            OciArtifactKind.Helm,
            plainHttp: true).ToArray();

        Assert.Equal("docker pull localhost:8080/docker/platform/api:2.1.0", dockerCommands[0]);
        Assert.Equal("oras pull --plain-http localhost:8080/docker/platform/api:2.1.0", dockerCommands[1]);
        Assert.Equal("crane pull --insecure localhost:8080/docker/platform/api:2.1.0 image.tar", dockerCommands[2]);
        Assert.Equal(
            "helm pull oci://localhost:8080/helm/charts/platform-stack --version 1.4.0 --plain-http",
            helmCommands[0]);
        Assert.Equal(
            "helm upgrade --install platform-stack oci://localhost:8080/helm/charts/platform-stack --version 1.4.0 --plain-http",
            helmCommands[1]);
        Assert.Equal(
            "oras pull --plain-http localhost:8080/helm/charts/platform-stack:1.4.0",
            helmCommands[2]);
    }

    [Fact]
    public void GenericOciCommands_UseOrasInsteadOfGuessingAnEcosystem()
    {
        var command = Assert.Single(OciCommandBuilder.BuildArtifactCommands(
            "packages.example.test/oci",
            "platform/sbom",
            "1.0.0",
            OciArtifactKind.Unknown));

        Assert.Equal("oras pull packages.example.test/oci/platform/sbom:1.0.0", command);
    }

    [Fact]
    public void LoginCommand_UsesPasswordStdin()
    {
        var command = OciCommandBuilder.BuildLogin(
            "docker login",
            "packages.example.test",
            "developer@example.test");

        Assert.Equal(
            "printf '%s' \"$REGISTRY_TOKEN\" | docker login packages.example.test --username \"developer@example.test\" --password-stdin",
            command);
        Assert.DoesNotContain(" -p ", command, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("docker login", true, false)]
    [InlineData("helm registry login", true, true)]
    [InlineData("oras login", true, true)]
    [InlineData("helm registry login", false, false)]
    [InlineData("oras login", false, false)]
    public void LoginCommand_UsesPlainHttpOnlyForSupportedClientsAndHttpFeeds(string client, bool plainHttp, bool expected)
    {
        var command = OciCommandBuilder.BuildLogin(client, "localhost:8080", "local@example.test", plainHttp);
        Assert.Equal(expected, command.Contains(" --plain-http", StringComparison.Ordinal));
        Assert.Contains("--password-stdin", command, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenPrompt_DoesNotPlaceTokenLiteralInShellHistory()
    {
        var prompt = OciCommandBuilder.BuildMaskedTokenPrompt();

        Assert.Contains("read -rs REGISTRY_TOKEN", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("read -rsp", prompt, StringComparison.Ordinal);
        Assert.Contains("REGISTRY_TOKEN", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("<token>", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("export ", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtocolNavigation_UsesDistinctBrowseAndConnectUrls()
    {
        Assert.Equal("/oci/docker", FeedProtocolPreference.ToHref("oci:docker"));
        Assert.Equal("/oci/connect/docker", FeedProtocolPreference.ConnectHref("oci:docker"));
        Assert.Equal("oci:docker", FeedProtocolPreference.TryInferFromPath("/oci/connect/docker"));
        Assert.Equal("oci", FeedProtocolPreference.TryInferFromPath("/oci/connect"));
    }

    [Fact]
    public void DockerTags_AreOrderedLatestThenByDescendingSemanticVersion()
    {
        var tags = new[] { "alpha", "1.2.0", "v2.0.0", "1.10.0", "latest", "2.0.0-beta.1" };

        var ordered = tags.OrderBy(tag => tag, OciTagVersionComparer.Instance).ToArray();

        Assert.Equal(
            ["latest", "v2.0.0", "2.0.0-beta.1", "1.10.0", "1.2.0", "alpha"],
            ordered);
    }

    [Fact]
    public void HelmVersions_AreOrderedByDescendingSemanticVersionBeforeFloatingLabels()
    {
        var tags = new[] { "alpha", "1.2.0", "v2.0.0", "1.10.0", "latest", "2.0.0-beta.1" };

        var ordered = tags.OrderBy(tag => tag, OciTagVersionComparer.HelmVersions).ToArray();

        Assert.Equal(
            ["v2.0.0", "2.0.0-beta.1", "1.10.0", "1.2.0", "latest", "alpha"],
            ordered);
    }

    [Fact]
    public void HelmCatalog_LabelsTheSelectedSemanticTagAsLatestVersion()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IOciRepositoryBrowseService>(new CatalogBrowseService(
            new OciRepositoryListItem(
                "charts/platform-stack",
                OciArtifactKind.Helm,
                LatestTag: "0.3.0",
                TagCount: 2)));
        context.Services
            .AddOptions<OciFeedOptions>(OciSurfaceOptionsBuilder.GetOptionsName("helm"))
            .Configure(options => options.Profile = OciProfile.HelmCharts);

        var component = context.Render<OciRepositoryCatalog>(parameters => parameters
            .Add(parameter => parameter.Segment, "helm"));

        component.WaitForAssertion(() =>
        {
            var terms = component.FindAll("dt").Select(element => element.TextContent.Trim()).ToArray();
            Assert.Contains("Latest version", terms);
            Assert.Contains("Versions", terms);
            Assert.DoesNotContain("Latest", terms);
            Assert.Contains("0.3.0", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Catalog_RestoresUnifiedSearchQueryAndPageFromTheUrl()
    {
        using var context = new BunitContext();
        var service = new CatalogBrowseService(new OciRepositoryListItem(
            "charts/platform-stack",
            OciArtifactKind.Helm,
            LatestTag: "0.3.0",
            TagCount: 2));
        context.Services.AddSingleton<IOciRepositoryBrowseService>(service);
        context.Services
            .AddOptions<OciFeedOptions>(OciSurfaceOptionsBuilder.GetOptionsName("helm"))
            .Configure(options => options.Profile = OciProfile.HelmCharts);
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("oci/helm?q=platform&page=2");

        var component = context.Render<OciRepositoryCatalog>(parameters => parameters
            .Add(parameter => parameter.Segment, "helm"));

        component.WaitForAssertion(() =>
        {
            Assert.Equal("platform", service.LastQuery);
            Assert.Equal(24, service.LastSkip);
            Assert.Equal("platform", component.Find("input[type=search]").GetAttribute("value"));
            Assert.Contains("Page 2", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void RepositoryDetail_SearchesAndPagesTheCompleteTrackedTagSet()
    {
        using var context = new BunitContext();
        var tags = Enumerable.Range(1, 63)
            .Select(index => new OciTagListItem(
                $"build-{index:D3}",
                Digest,
                OciArtifactKind.Image,
                512,
                new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
                "linux",
                "amd64",
                PackageOrigin.Published))
            .ToArray();
        var service = new TagBrowseService(tags);
        context.Services.AddSingleton<IOciRepositoryBrowseService>(service);
        context.Services
            .AddOptions<OciFeedOptions>(OciSurfaceOptionsBuilder.GetOptionsName("docker"))
            .Configure(options => options.Profile = OciProfile.ContainerImages);
        context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("oci/docker/repos/demo/app?page=2");

        var component = context.Render<OciRepositoryDetail>(parameters => parameters
            .Add(parameter => parameter.RepositoryName, "demo/app")
            .Add(parameter => parameter.Segment, "docker"));

        component.WaitForAssertion(() =>
        {
            Assert.Null(service.LastMax);
            Assert.Contains("26–50 of 63 tags", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Page 2 of 3", component.Markup, StringComparison.Ordinal);
            Assert.Equal(25, component.FindAll("a[href*='reference=']").Count);
            Assert.Contains("build-026", component.Markup, StringComparison.Ordinal);
            Assert.Contains("build-050", component.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("build-025", component.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("build-051", component.Markup, StringComparison.Ordinal);
        });

        component.Find("input[type=search]").Input("build-063");
        component.Find("form").Submit();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("1 tag", component.Markup, StringComparison.Ordinal);
            Assert.Single(component.FindAll("a[href*='reference=']"));
            Assert.Contains("build-063", component.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Page 1 of", component.Markup, StringComparison.Ordinal);
            Assert.Contains("q=build-063", context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void HelmArtifact_RendersDigestCommandsWithoutClaimingProvenance()
    {
        using var context = new BunitContext();
        var artifact = new OciArtifactDetailModel(
            "platform/api",
            Digest,
            Digest,
            "application/vnd.oci.image.manifest.v1+json",
            OciArtifactKind.Helm,
            null,
            null,
            512,
            ["2.1.0"],
            "https://packages.example.test/helm/v2/",
            new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
            PackageOrigin.Published,
            2);

        var component = context.Render<OciArtifactDetail>(parameters => parameters
            .Add(parameter => parameter.Artifact, artifact)
            .Add(parameter => parameter.Segment, "helm"));

        Assert.Contains($"oci://packages.example.test/helm/platform/api@{Digest}", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("provenance", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("crane pull", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Image configuration", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Image layers", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void HelmArtifact_RendersEcosystemMetadataAndSafelyEncodedChartDocuments()
    {
        using var context = new BunitContext();
        var details = new HelmChartDetails(
            "platform-stack",
            "1.4.0",
            "9.2.1",
            "Deploys the unified package platform.",
            "v2",
            ">=1.29.0-0",
            "application",
            "https://example.test/charts/platform-stack",
            "https://example.test/icons/platform-stack.svg",
            false,
            ["https://example.test/source", "javascript:alert('source')"],
            ["packages", "registry"],
            [new HelmChartMaintainer("Platform Team", "team@example.test", "javascript:alert('maintainer')")],
            [new HelmChartDependency(
                "postgresql",
                "15.x.x",
                "https://charts.example.test",
                "postgresql.enabled",
                null,
                ["database"])],
            "<h1>Platform Stack</h1><p>Rendered README</p>",
            "message: <script>alert('x')</script>\n",
            "{\n  \"type\": \"object\"\n}",
            "apiVersion: v2\nname: platform-stack\n",
            "dependencies: []\n",
            true,
            null);
        var artifact = new OciArtifactDetailModel(
            "charts/platform-stack",
            "1.4.0",
            Digest,
            "application/vnd.oci.image.manifest.v1+json",
            OciArtifactKind.Helm,
            null,
            null,
            512,
            ["1.4.0"],
            "https://packages.example.test/helm/v2/",
            new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
            PackageOrigin.Published,
            1,
            HelmDetails: details);

        var component = context.Render<OciArtifactDetail>(parameters => parameters
            .Add(parameter => parameter.Artifact, artifact)
            .Add(parameter => parameter.Segment, "helm"));

        Assert.Contains("platform-stack 1.4.0", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Deploys the unified package platform.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Chart version", component.Markup, StringComparison.Ordinal);
        Assert.Contains("postgresql.enabled", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Rendered README", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Default values", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Values schema", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Dependency lock", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Provenance detected · not verified", component.Markup, StringComparison.Ordinal);
        Assert.Contains("has not been cryptographically verified", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Verify chart provenance", component.Markup, StringComparison.Ordinal);
        Assert.Contains(
            $"helm pull oci://packages.example.test/helm/charts/platform-stack@{Digest} --verify",
            component.Markup,
            StringComparison.Ordinal);
        Assert.Contains("keys in your keyring", component.Markup, StringComparison.Ordinal);
        Assert.Contains("src=\"https://example.test/icons/platform-stack.svg\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("alt=\"platform-stack chart icon\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert('x')&lt;/script&gt;", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert('x')</script>", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HelmArtifact_DoesNotRenderUnsafeChartIcon()
    {
        using var context = new BunitContext();
        var details = new HelmChartDetails(
            "platform-stack",
            "1.4.0",
            null,
            null,
            "v2",
            null,
            null,
            null,
            "javascript:alert('icon')",
            false,
            [],
            [],
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            false,
            null);
        var artifact = new OciArtifactDetailModel(
            "charts/platform-stack",
            "1.4.0",
            Digest,
            "application/vnd.oci.image.manifest.v1+json",
            OciArtifactKind.Helm,
            null,
            null,
            512,
            ["1.4.0"],
            "https://packages.example.test/helm/v2/",
            null,
            PackageOrigin.Published,
            1,
            HelmDetails: details);

        var component = context.Render<OciArtifactDetail>(parameters => parameters
            .Add(parameter => parameter.Artifact, artifact)
            .Add(parameter => parameter.Segment, "helm"));

        Assert.DoesNotContain("<img", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DockerArtifact_RendersStoredRuntimeMetadataLayersAndHistory()
    {
        using var context = new BunitContext();
        var details = new OciDockerImageDetails(
            new DateTimeOffset(2026, 9, 7, 10, 15, 30, TimeSpan.Zero),
            "linux",
            "arm64",
            "v8",
            ["dotnet"],
            ["App.dll", "--serve"],
            "/app",
            "10001",
            new Dictionary<string, string>
            {
                ["org.opencontainers.image.source"] = "https://example.test/repo",
                ["org.example.unsafe"] = "<script>alert(1)</script>",
            },
            [new OciDockerLayer(1, Digest, "application/vnd.oci.image.layer.v1.tar+gzip", 2048)],
            [new OciDockerHistoryEntry(null, "ENTRYPOINT [dotnet]", null, true)],
            [],
            new Dictionary<string, string>
            {
                ["org.opencontainers.image.source"] = "https://example.test/manifest-source",
                ["org.opencontainers.image.revision"] = "e1c9b4a",
                ["org.opencontainers.image.base.name"] = "mcr.microsoft.com/dotnet/aspnet:10.0",
                ["org.opencontainers.image.base.digest"] = "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                ["org.opencontainers.image.licenses"] = "Apache-2.0",
                ["org.opencontainers.image.documentation"] = "javascript:alert('docs')",
            },
            [
                new OciDockerSupplyChainArtifact(
                    OciDockerSupplyChainArtifactKind.Signature,
                    "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.sig",
                    "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                new OciDockerSupplyChainArtifact(
                    OciDockerSupplyChainArtifactKind.Sbom,
                    "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.sbom",
                    "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                new OciDockerSupplyChainArtifact(
                    OciDockerSupplyChainArtifactKind.VulnerabilityReport,
                    "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                    "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                    "application/vnd.aquasec.trivy.report+json",
                    IsOciReferrer: true,
                    VulnerabilitySummary: new OciVulnerabilityReportSummary(
                        "Trivy",
                        Critical: 1,
                        High: 2,
                        Medium: 3,
                        Low: 0,
                        Negligible: 0,
                        Unknown: 0)),
            ],
            Digest,
            new OciVulnerabilityPolicyAssessment(
                OciVulnerabilityPolicyStatus.Failed,
                "Reported findings exceed the configured severity limits.",
                ["1 critical reported; maximum allowed is 0."]));
        var artifact = new OciArtifactDetailModel(
            "platform/api",
            "2.1.0",
            Digest,
            "application/vnd.oci.image.manifest.v1+json",
            OciArtifactKind.Image,
            "linux",
            "arm64",
            512,
            ["2.1.0"],
            "https://packages.example.test/docker/v2/",
            new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
            PackageOrigin.Published,
            1,
            details);

        var component = context.Render<OciArtifactDetail>(parameters => parameters
            .Add(parameter => parameter.Artifact, artifact)
            .Add(parameter => parameter.Segment, "docker"));

        Assert.Contains("Image configuration", component.Markup, StringComparison.Ordinal);
        Assert.Contains("linux/arm64/v8", component.Markup, StringComparison.Ordinal);
        Assert.Contains("App.dll", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Image labels", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Image layers", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Build history", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Supply-chain evidence", component.Markup, StringComparison.Ordinal);
        Assert.Contains("OCI 1.1 referrers", component.Markup, StringComparison.Ordinal);
        Assert.Contains("SHA-256 content addressed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Publisher identity", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Not verified", component.Markup, StringComparison.Ordinal);
        Assert.Contains("1 critical", component.Markup, StringComparison.Ordinal);
        Assert.Contains("2 high", component.Markup, StringComparison.Ordinal);
        Assert.Contains("3 medium", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Trivy report", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Policy failed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("maximum allowed is 0", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Vulnerability report", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Declared provenance", component.Markup, StringComparison.Ordinal);
        Assert.Contains("e1c9b4a", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Base image", component.Markup, StringComparison.Ordinal);
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet:10.0", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Apache-2.0", component.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.test/manifest-source\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Signature", component.Markup, StringComparison.Ordinal);
        Assert.Contains("SBOM", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Stored, not verified", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Verify with your trust policy", component.Markup, StringComparison.Ordinal);
        Assert.Contains(
            $"cosign verify --key /path/to/trusted-cosign.pub packages.example.test/docker/platform/api@{Digest}",
            component.Markup,
            StringComparison.Ordinal);
        Assert.Contains(
            "oras pull packages.example.test/docker/platform/api:sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.sbom",
            component.Markup,
            StringComparison.Ordinal);
        Assert.Contains(
            "oras pull packages.example.test/docker/platform/api@sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            component.Markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain("cosign download sbom", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("cosign verify-attestation", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"javascript:", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DockerArtifact_CopyButtonWritesExactPullCommand()
    {
        using var context = new BunitContext();
        const string command = "docker pull packages.example.test/docker/platform/api:2.1.0";
        context.JSInterop
            .SetupVoid("navigator.clipboard.writeText", command)
            .SetVoidResult();
        var artifact = new OciArtifactDetailModel(
            "platform/api",
            "2.1.0",
            Digest,
            "application/vnd.oci.image.manifest.v1+json",
            OciArtifactKind.Image,
            "linux",
            "amd64",
            512,
            ["2.1.0"],
            "https://packages.example.test/docker/v2/",
            null,
            PackageOrigin.Published,
            1,
            DockerDetails: OciDockerMetadataReader.ToDetails(OciDockerManifestMetadata.Empty) with
            {
                ManifestDigest = Digest,
            });

        var component = context.Render<OciArtifactDetail>(parameters => parameters
            .Add(parameter => parameter.Artifact, artifact)
            .Add(parameter => parameter.Segment, "docker"));

        component.Find("button[aria-label='Copy Docker command']").Click();

        var invocation = context.JSInterop.VerifyInvoke("navigator.clipboard.writeText");
        Assert.Equal(command, Assert.Single(invocation.Arguments));
        Assert.Contains("Published here", component.Markup, StringComparison.Ordinal);
        Assert.Contains("No legacy Cosign signature, attestation, or SBOM tag is indexed", component.Markup, StringComparison.Ordinal);
    }

    private sealed class CatalogBrowseService(OciRepositoryListItem item) : IOciRepositoryBrowseService
    {
        public string? LastQuery { get; private set; }

        public int LastSkip { get; private set; }

        public Task<OciRepositorySearchResult> SearchRepositoriesAsync(
            string? segment,
            string? query = null,
            string? kind = null,
            int skip = 0,
            int take = 24,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            LastSkip = skip;
            return Task.FromResult(new OciRepositorySearchResult([item], skip + 1, skip, take));
        }

        public Task<IReadOnlyList<OciRepositoryListItem>> ListRepositoriesAsync(
            string? segment,
            int? max = null,
            string? last = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OciRepositoryTagsModel?> ListTagsAsync(
            string repositoryName,
            string? segment,
            int? max = null,
            string? last = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OciArtifactDetailModel?> GetArtifactAsync(
            string repositoryName,
            string reference,
            string? segment,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public string GetRegistryRootUrl(string? segment) => "https://packages.example.test/helm/v2/";

        public string GetRegistryApiUrl(string? segment) => GetRegistryRootUrl(segment);
    }

    private sealed class TagBrowseService(IReadOnlyList<OciTagListItem> tags) : IOciRepositoryBrowseService
    {
        public int? LastMax { get; private set; }

        public Task<OciRepositorySearchResult> SearchRepositoriesAsync(
            string? segment,
            string? query = null,
            string? kind = null,
            int skip = 0,
            int take = 24,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<OciRepositoryListItem>> ListRepositoriesAsync(
            string? segment,
            int? max = null,
            string? last = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OciRepositoryTagsModel?> ListTagsAsync(
            string repositoryName,
            string? segment,
            int? max = null,
            string? last = null,
            CancellationToken cancellationToken = default)
        {
            LastMax = max;
            return Task.FromResult<OciRepositoryTagsModel?>(new(
                repositoryName,
                OciArtifactKind.Image,
                tags));
        }

        public Task<OciArtifactDetailModel?> GetArtifactAsync(
            string repositoryName,
            string reference,
            string? segment,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public string GetRegistryRootUrl(string? segment) => "https://packages.example.test/docker/v2/";

        public string GetRegistryApiUrl(string? segment) => GetRegistryRootUrl(segment);
    }
}
