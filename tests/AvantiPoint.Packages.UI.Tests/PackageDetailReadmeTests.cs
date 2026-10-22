using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.Configuration;
using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class PackageDetailReadmeTests
{
    [Fact]
    public void RendersLiveProxyUpstreamAsMetadataOnlyWithoutClaimingStorageOrDownloadCounts()
    {
        using var context = new BunitContext();
        context.Services.AddHttpClient();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton<IPackageDiffService>(new UnusedDiffService());

        var package = new PackageInfoCollection("2.1.0")
        {
            Versions =
            [
                new PackageInfo
                {
                    PackageId = "Authenticated.Upstream.Package",
                    Version = NuGetVersion.Parse("2.1.0"),
                    Authors = ["Upstream Publisher"],
                    Description = "Live upstream metadata",
                    Dependencies = new Dictionary<string, IEnumerable<PackageDependencyInfo>>(),
                    IsListed = true,
                    Published = DateTimeOffset.UtcNow.AddDays(-1),
                    Origin = "Upstream",
                    PackageSourceName = "Telerik authenticated feed",
                    MetadataFetchedAt = DateTimeOffset.UtcNow,
                    UpstreamCachingStrategy = "ProxyOnly",
                }
            ]
        };

        var component = context.Render<PackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.FeedBaseUrl, "https://packages.example.test"));

        Assert.Contains("Available upstream", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Telerik authenticated feed", component.Markup, StringComparison.Ordinal);
        Assert.Contains("checked live by exact package ID", component.Markup, StringComparison.Ordinal);
        Assert.Contains("upstream catalog was not copied", component.Markup, StringComparison.Ordinal);
        Assert.Contains("not retained", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Metadata checked live", component.Markup, StringComparison.Ordinal);
        Assert.Contains("README content is not fetched", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Download counts are unavailable", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Copied from an upstream source", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RendersProvidedReadmeWithoutCallingThePublicFeedUrl()
    {
        using var context = new BunitContext();
        context.Services.AddHttpClient();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton<IPackageDiffService>(new UnusedDiffService());

        var package = new PackageInfoCollection("1.0.0")
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
                    HasReadme = true,
                    IsListed = true,
                    Published = DateTimeOffset.UtcNow,
                    Origin = "Cached",
                    PackageSourceName = "Authenticated upstream",
                    PackageSourceLastSyncSuccessAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                    RepositoryType = "git",
                    RepositoryUrl = "https://example.test/source"
                }
            ]
        };

        var component = context.Render<PackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.FeedBaseUrl, "http://public-address-that-is-not-reachable.invalid")
            .Add(p => p.ReadmeMarkdown, "# Package readme\n\n**Rendered locally.**\n\n<script>alert('unsafe')</script>"));

        Assert.Contains("Package readme", component.Markup, StringComparison.Ordinal);
        Assert.Contains("<strong>Rendered locally.</strong>", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Source<", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">git<", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Download package", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Download symbols", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Cached on demand", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Authenticated upstream", component.Markup, StringComparison.Ordinal);
        Assert.Contains("the upstream catalog was not copied", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Upstream last reached", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("from now", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

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
            throw new InvalidOperationException("The readme test must not load a package diff.");
    }
}
