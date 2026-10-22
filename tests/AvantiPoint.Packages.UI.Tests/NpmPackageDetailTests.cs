using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.UI.Components.Npm;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Components;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NpmPackageDetailTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void InstallCommands_RenderSelectedVersionAndUpdateWhenVersionChanges()
    {
        var package = new NpmPackageDetailModel(
            "kind-of",
            [
                new NpmVersionListItem("6.0.3", new DateTime(2020, 1, 16), "sha-latest"),
                new NpmVersionListItem("5.1.0", new DateTime(2017, 10, 13), "sha-old"),
            ],
            new Dictionary<string, string> { ["latest"] = "6.0.3" },
            "Get the native type of a value.");

        var component = _context.Render<NpmPackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.RegistryUrl, "https://packages.example/npm/"));

        Assert.Equal("kind-of", component.Find("h1").TextContent);
        Assert.Contains("npm install kind-of@6.0.3", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@SelectedVersion", component.Markup, StringComparison.Ordinal);

        SelectVersion(component, "5.1.0");

        Assert.Contains("npm install kind-of@5.1.0", component.Markup, StringComparison.Ordinal);
        component.FindAll("[aria-label='Package manager'] button").Single(b => b.TextContent == "pnpm").Click();
        Assert.Contains("pnpm add kind-of@5.1.0", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("npm install kind-of@5.1.0", component.Markup, StringComparison.Ordinal);
        component.FindAll("[aria-label='Package manager'] button").Single(b => b.TextContent == "Yarn").Click();
        Assert.Contains("yarn add kind-of@5.1.0", component.Markup, StringComparison.Ordinal);
        Assert.EndsWith("?version=5.1.0", _context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.Single(component.FindAll("button[aria-label^='Copy'][aria-label$='install command']"));
    }

    [Fact]
    public void InteractiveVersionSelection_UpdatesPresentationAndVersionMetadataTogether()
    {
        var current = CreateRichVersion("2.0.0", "Current", "current");
        var legacy = CreateRichVersion("1.0.0", "Legacy", "legacy");
        var package = new NpmPackageDetailModel(
            "example-package",
            [current, legacy],
            new Dictionary<string, string> { ["latest"] = current.Version });
        var component = _context.Render<NpmPackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.RegistryUrl, "https://packages.example/npm/"));

        AssertSelectedVersionPresentation(component, current, "Current", "current");

        SelectVersion(component, legacy.Version);

        AssertSelectedVersionPresentation(component, legacy, "Legacy", "legacy");
        Assert.DoesNotContain("Current description", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("current-dependency", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Current readme</h1>", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyInstallCommand_WritesExactSelectedVersionCommandToClipboard()
    {
        const string expected = "npm install kind-of@6.0.3 --registry https://packages.example/npm/";
        _context.JSInterop
            .SetupVoid("navigator.clipboard.writeText", expected)
            .SetVoidResult();
        var package = new NpmPackageDetailModel(
            "kind-of",
            [new NpmVersionListItem("6.0.3", new DateTime(2020, 1, 16), "sha-latest")],
            new Dictionary<string, string> { ["latest"] = "6.0.3" });
        var component = _context.Render<NpmPackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.RegistryUrl, "https://packages.example/npm/"));

        component.Find("button[aria-label='Copy npm install command']").Click();

        var invocation = _context.JSInterop.VerifyInvoke("navigator.clipboard.writeText");
        Assert.Equal(expected, Assert.Single(invocation.Arguments));
    }

    [Fact]
    public void PackageMetadata_RendersNpmNativeDetailsForSelectedVersion()
    {
        var package = new NpmPackageDetailModel(
            "@avantipoint/widgets",
            [
                new NpmVersionListItem(
                    "2.1.0",
                    new DateTime(2026, 8, 1, 12, 30, 0, DateTimeKind.Utc),
                    "f00dcafe",
                    Origin: PackageOrigin.Mirrored,
                    Deprecated: "Use @avantipoint/components instead.",
                    Integrity: "sha512-AbCdEf==",
                    TarballFileName: "widgets-2.1.0.tgz",
                    UnpackedSize: 2048,
                    FileCount: 12,
                    Dependencies: new Dictionary<string, string> { ["kleur"] = "^4.1.5" },
                    PeerDependencies: new Dictionary<string, string> { ["react"] = ">=18" },
                    OptionalPeerDependencies: ["react"],
                    Engines: new Dictionary<string, string> { ["node"] = ">=20" },
                    PackageManager: "npm@11.0.0",
                    ModuleType: "module",
                    TypesEntryPoint: "./dist/index.d.ts",
                    Publisher: new NpmPersonModel("package-publisher", "publisher@example.test"),
                    PublishingNpmVersion: "11.4.2",
                    PublishingNodeVersion: "22.14.0",
                    GitHead: "0123456789abcdef",
                    RegistrySignatureCount: 2,
                    Provenance: new NpmProvenanceModel(
                        "https://registry.example/-/npm/v1/attestations/widgets@2.1.0",
                        "https://slsa.dev/provenance/v1")),
            ],
            new Dictionary<string, string> { ["latest"] = "2.1.0" },
            "Composable UI widgets.",
            ["widgets", "ui"],
            "# Widgets\n\nUse **composition**.\n\n<script>alert('unsafe')</script>\n\n[unsafe](javascript:alert('x'))",
            "README.md",
            "MIT",
            "https://example.test/widgets",
            "https://github.com/avantipoint/widgets",
            "packages/widgets",
            "https://github.com/avantipoint/widgets/issues",
            "support@example.test",
            new NpmPersonModel("AvantiPoint"),
            [new NpmPersonModel("Package Team", "packages@example.test")],
            [new NpmFundingLinkModel("https://github.com/sponsors/avantipoint", "GitHub Sponsors")]);

        var component = _context.Render<NpmPackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.RegistryUrl, "https://packages.example/npm/"));

        Assert.Contains("This version is deprecated", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Use @avantipoint/components instead.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("kleur", component.Markup, StringComparison.Ordinal);
        Assert.Contains("react", component.Markup, StringComparison.Ordinal);
        Assert.Contains("optional", component.Markup, StringComparison.Ordinal);
        Assert.Contains("sha512-AbCdEf==", component.Markup, StringComparison.Ordinal);
        Assert.Contains("2 KB", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Mirrored from upstream", component.Markup, StringComparison.Ordinal);
        Assert.Contains("MIT", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Package Team", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Trust and provenance", component.Markup, StringComparison.Ordinal);
        Assert.Contains("package-publisher", component.Markup, StringComparison.Ordinal);
        Assert.Contains("0123456789abcdef", component.Markup, StringComparison.Ordinal);
        Assert.Contains("2 detected · not verified by this feed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Attestation metadata detected · not verified by this feed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("https://slsa.dev/provenance/v1", component.Markup, StringComparison.Ordinal);
        Assert.Contains(
            "npm audit signatures --registry https://packages.example/npm/",
            component.Markup,
            StringComparison.Ordinal);
        var tarball = component.Find("a[download]");
        Assert.Equal(
            "https://packages.example/npm/%40avantipoint/widgets/-/widgets-2.1.0.tgz",
            tarball.GetAttribute("href"));
        Assert.Equal("widgets-2.1.0.tgz", tarball.GetAttribute("download"));
        Assert.Contains("href=\"/browse/npm/packages/kleur\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("<h1", component.Markup, StringComparison.Ordinal);
        Assert.Contains(">Widgets</h1>", component.Markup, StringComparison.Ordinal);
        Assert.Contains("<strong>composition</strong>", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VersionQuery_SelectsTheVersionAndItsPresentationMetadata()
    {
        _context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/browse/npm/packages/example-package?version=1.0.0");
        var current = CreateRichVersion("2.0.0", "Current", "current");
        var legacy = CreateRichVersion("1.0.0", "Legacy", "legacy");
        var package = new NpmPackageDetailModel(
            "example-package",
            [current, legacy],
            new Dictionary<string, string> { ["latest"] = current.Version });

        var component = _context.Render<NpmPackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.RegistryUrl, "https://packages.example/npm/"));

        AssertSelectedVersionPresentation(component, legacy, "Legacy", "legacy");
        Assert.Contains(legacy.Version, component.Find("#npm-version-picker").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Current description", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("current-dependency", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Current readme</h1>", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionMetadata_UsesPackageFallbackOnlyForMissingFields()
    {
        var version = new NpmVersionListItem(
            "1.0.0",
            new DateTime(2026, 1, 1),
            "sha",
            Metadata: new NpmPackagePresentationMetadata(
                Description: "Version description",
                Keywords: []));
        var package = new NpmPackageDetailModel(
            "example-package",
            [version],
            new Dictionary<string, string> { ["latest"] = version.Version },
            Description: "Fallback description",
            Keywords: ["fallback-keyword"],
            License: "MIT",
            RepositoryUrl: "https://example.test/fallback/repository");

        var component = _context.Render<NpmPackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.RegistryUrl, "https://packages.example/npm/"));

        Assert.Contains("Version description", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Fallback description", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback-keyword", component.Markup, StringComparison.Ordinal);
        Assert.Contains("MIT", component.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.test/fallback/repository\"", component.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://packages.example/npm/", "//packages.example/npm/:_authToken=${NPM_TOKEN}")]
    [InlineData("https://packages.example/", "//packages.example/:_authToken=${NPM_TOKEN}")]
    [InlineData("/custom/npm", "//localhost/custom/npm/:_authToken=${NPM_TOKEN}")]
    public void RegistryAuthLine_UsesNpmrcPathSyntax(string registryUrl, string expected)
    {
        Assert.Equal(expected, NpmRegistryInstructions.BuildAuthTokenLine(registryUrl));
    }

    [Fact]
    public void LargeVersionHistory_RendersBoundedSearchablePickersAndPreservesExactUrlState()
    {
        _context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/browse/npm/packages/%40avantipoint/long-history");
        var versions = Enumerable.Range(0, 3_000)
            .Select(index => new NpmVersionListItem(
                $"{index}.0.0",
                new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(-index),
                $"sha-{index}"))
            .ToList();
        const string latest = "2999.0.0";
        const string oldVersion = "1729.0.0";
        var package = new NpmPackageDetailModel(
            "@avantipoint/long-history",
            versions,
            new Dictionary<string, string>
            {
                ["latest"] = latest,
                ["legacy"] = oldVersion,
            });
        var component = _context.Render<NpmPackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.RegistryUrl, "https://packages.example/npm/"));

        Assert.Empty(component.FindAll("select option"));

        component.Find("#npm-version-picker").Click();
        Assert.Equal(24, component.FindAll("#npm-version-picker-results [data-npm-version-option]").Count);
        Assert.Contains("first 24 of 3,000", component.Find("#npm-version-picker-status").TextContent, StringComparison.Ordinal);
        component.Find("#npm-version-picker-search").Input(oldVersion);
        var exactVersion = component.Find($"[data-npm-version-option='{oldVersion}']");
        Assert.Contains(oldVersion, exactVersion.TextContent, StringComparison.Ordinal);

        component.Find("#npm-compare-version-picker").Click();
        Assert.Equal(24, component.FindAll("#npm-compare-version-picker-results [data-npm-version-option]").Count);
        component.Find("#npm-compare-version-picker-search").Input(oldVersion);
        component.Find($"#npm-compare-version-picker-results [data-npm-version-option='{oldVersion}']").Click();

        var navigation = _context.Services.GetRequiredService<NavigationManager>();
        Assert.Contains("version=2999.0.0", navigation.Uri, StringComparison.Ordinal);
        Assert.Contains("compare=1729.0.0", navigation.Uri, StringComparison.Ordinal);
        Assert.Contains("/browse/npm/packages/%40avantipoint/long-history", navigation.Uri, StringComparison.Ordinal);
    }

    private static NpmVersionListItem CreateRichVersion(
        string version,
        string label,
        string slug) =>
        new(
            version,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            $"{slug}-sha",
            Integrity: $"sha512-{slug}",
            TarballFileName: $"example-package-{version}.tgz",
            Dependencies: new Dictionary<string, string> { [$"{slug}-dependency"] = "^1.0.0" },
            Metadata: new NpmPackagePresentationMetadata(
                Description: $"{label} description",
                Keywords: [$"{slug}-keyword"],
                Readme: $"# {label} readme",
                ReadmeFileName: $"README-{slug}.md",
                License: $"{label}-License",
                HomepageUrl: $"https://example.test/{slug}/home",
                RepositoryUrl: $"https://example.test/{slug}/repository",
                RepositoryDirectory: $"packages/{slug}",
                BugsUrl: $"https://example.test/{slug}/issues",
                BugsEmail: $"{slug}@example.test",
                Author: new NpmPersonModel($"{label} Author"),
                Maintainers: [new NpmPersonModel($"{label} Maintainer")],
                Funding: [new NpmFundingLinkModel($"https://example.test/{slug}/funding", $"{label} Funding")]));

    private static void SelectVersion(
        IRenderedComponent<NpmPackageDetail> component,
        string version)
    {
        component.Find("#npm-version-picker").Click();
        component.Find("#npm-version-picker-search").Input(version);
        component.Find($"#npm-version-picker-results [data-npm-version-option='{version}']").Click();
    }

    private static void AssertSelectedVersionPresentation(
        IRenderedComponent<NpmPackageDetail> component,
        NpmVersionListItem version,
        string label,
        string slug)
    {
        Assert.Contains($"npm install example-package@{version.Version}", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"{label} description", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"{slug}-keyword", component.Markup, StringComparison.Ordinal);
        Assert.Contains($">{label} readme</h1>", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"{label}-License", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"href=\"https://example.test/{slug}/home\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"href=\"https://example.test/{slug}/repository\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"href=\"https://example.test/{slug}/issues\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"href=\"https://example.test/{slug}/funding\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"packages/{slug}", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"{slug}@example.test", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"{label} Author", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"{label} Maintainer", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"{label} Funding", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"{slug}-dependency", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"sha512-{slug}", component.Markup, StringComparison.Ordinal);
        Assert.Equal(
            $"example-package-{version.Version}.tgz",
            component.Find("a[download]").GetAttribute("download"));
    }

    public void Dispose() => _context.Dispose();
}
