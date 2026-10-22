using Microsoft.Extensions.Configuration;
using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NuGetTrustPresentationTests
{
    [Fact]
    public void SearchResultShowsVerifiedOwnerAndKnownVulnerabilitiesPrecisely()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var package = CreateSearchResult();
        package.Verified = true;
        package.Vulnerabilities =
        [
            new PackageVulnerabilityInfo { Severity = "High", Url = "https://example.test/advisories/1", Versions = "[1.0.0]" },
            new PackageVulnerabilityInfo { Severity = "Critical", Url = "https://example.test/advisories/2", Versions = "[1.0.0]" }
        ];

        var component = context.Render<PackageSearchResultItem>(parameters => parameters
            .Add(p => p.Package, package));

        Assert.Contains("✓ Verified owner", component.Markup, StringComparison.Ordinal);
        Assert.Contains("verified reserved prefix", component.Markup, StringComparison.Ordinal);
        Assert.Contains("2 known vulnerabilities", component.Markup, StringComparison.Ordinal);
        Assert.Contains("High, Critical", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchResultDoesNotImplyVerificationOrVulnerabilitiesWhenUnknown()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var package = CreateSearchResult();

        var component = context.Render<PackageSearchResultItem>(parameters => parameters
            .Add(p => p.Package, package));

        Assert.DoesNotContain("Verified owner", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("known vulnerability", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailShowsDeprecationAndSanitizedReleaseNotesForSelectedVersion()
    {
        using var context = CreateDetailContext();
        var package = CreatePackage(
            projectUrl: "https://example.test/project",
            repositoryUrl: "https://example.test/source",
            licenseUrl: "https://example.test/license",
            isDeprecated: true,
            releaseNotes: "# Important update\n\n**Upgrade before deploying.**\n\n<script>alert('unsafe')</script>",
            deprecationReasons: ["Legacy", "CriticalBugs"],
            deprecationMessage: "Use the supported replacement before deploying.",
            alternatePackageId: "Example.Package.Next",
            alternateVersionRange: "[2.0.0, )");

        var component = context.Render<PackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.Tab, "release-notes"));

        Assert.Contains("This package version is deprecated", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Use the supported replacement before deploying.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Reasons: Legacy, CriticalBugs", component.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/packages/Example.Package.Next\"", component.Markup, StringComparison.Ordinal);
        Assert.Contains("[2.0.0, )", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Release notes", component.Markup, StringComparison.Ordinal);
        Assert.Contains(">Important update</h1>", component.Markup, StringComparison.Ordinal);
        Assert.Contains("<strong>Upgrade before deploying.</strong>", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetailOnlyLinksToSafeWebUrls()
    {
        using var context = CreateDetailContext();
        var unsafePackage = CreatePackage(
            projectUrl: "javascript:alert('project')",
            repositoryUrl: "file:///etc/passwd",
            licenseUrl: "data:text/html,unsafe");

        var unsafeComponent = context.Render<PackageDetail>(parameters => parameters
            .Add(p => p.Package, unsafePackage));

        Assert.DoesNotContain("javascript:", unsafeComponent.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file:///", unsafeComponent.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text/html", unsafeComponent.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Website<", unsafeComponent.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Source<", unsafeComponent.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">View license<", unsafeComponent.Markup, StringComparison.Ordinal);

        var safePackage = CreatePackage(
            projectUrl: "https://example.test/project",
            repositoryUrl: "http://example.test/source",
            licenseUrl: "https://example.test/license");
        var safeComponent = context.Render<PackageDetail>(parameters => parameters
            .Add(p => p.Package, safePackage));

        Assert.Contains("href=\"https://example.test/project\"", safeComponent.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"http://example.test/source\"", safeComponent.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.test/license\"", safeComponent.Markup, StringComparison.Ordinal);
    }

    private static SearchResult CreateSearchResult() => new()
    {
        PackageId = "Example.Package",
        Version = "1.0.0",
        Title = "Example.Package",
        Description = "A package used to test trust presentation.",
        Authors = ["AvantiPoint"],
        Owners = ["AvantiPoint"],
        Tags = ["test"],
        PackageTypes = [],
        Versions = [],
        ResourceTypes = ["Package"],
        Published = DateTimeOffset.UtcNow,
        Vulnerabilities = []
    };

    private static BunitContext CreateDetailContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddHttpClient();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton<IPackageDiffService>(new UnusedDiffService());
        return context;
    }

    private static PackageInfoCollection CreatePackage(
        string? projectUrl = null,
        string? repositoryUrl = null,
        string? licenseUrl = null,
        bool isDeprecated = false,
        string? releaseNotes = null,
        IReadOnlyList<string>? deprecationReasons = null,
        string? deprecationMessage = null,
        string? alternatePackageId = null,
        string? alternateVersionRange = null) => new("1.0.0")
    {
        Versions =
        [
            new PackageInfo
            {
                PackageId = "Example.Package",
                Version = NuGetVersion.Parse("1.0.0"),
                Authors = ["AvantiPoint"],
                Description = "Example package",
                Dependencies = new Dictionary<string, IEnumerable<PackageDependencyInfo>>(),
                IsListed = true,
                IsDeprecated = isDeprecated,
                DeprecationReasons = deprecationReasons ?? [],
                DeprecationMessage = deprecationMessage!,
                DeprecatedAlternatePackageId = alternatePackageId!,
                DeprecatedAlternatePackageVersionRange = alternateVersionRange!,
                Published = DateTimeOffset.UtcNow,
                ProjectUrl = projectUrl!,
                RepositoryUrl = repositoryUrl!,
                LicenseUrl = licenseUrl!,
                ReleaseNotes = releaseNotes!
            }
        ]
    };

    private sealed class UnusedDiffService : IPackageDiffService
    {
        public Task<PackageDiffResult> GetDiffAsync(
            string packageId,
            string currentVersion,
            string? compareVersion,
            bool onlyOneVersionInFeed,
            string? filePath = null,
            string? view = null,
            string? tfm = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("This test must not load a package diff.");
    }
}
