using System.Net;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Routing;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Integration.Tests.TestInfrastructure;
using AvantiPoint.Packages.Registry.Tests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Integration.Tests;

public sealed class CrossFeedIsolationIntegrationTests
{
    [Theory]
    [InlineData("/v3/index.json", FeedProtocol.NuGet)]
    [InlineData("/api/v3/search", FeedProtocol.NuGet)]
    [InlineData("/npm/-/v1/search", FeedProtocol.Npm)]
    public void FeedSurfaceMatcher_RoutesProtocolsCorrectly(string path, FeedProtocol protocol)
    {
        var registry = CreateMultiSurfaceRegistry();
        var match = FeedSurfaceMatcher.Match(registry, new PathString(path));
        Assert.NotNull(match);
        Assert.Equal(protocol, match.Registration.Protocol);
    }

    [Fact]
    public async Task NuGetSearch_ExcludesPackagesFromOtherFeedId()
    {
        await using var feed = await FeedUnderTestHost.StartAsync(new FeedUnderTestOptions());

        using (var scope = feed.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            context.Packages.Add(new Package
            {
                FeedId = "other-feed",
                Id = "Other.Feed.Package",
                Version = NuGet.Versioning.NuGetVersion.Parse("1.0.0"),
                NormalizedVersionString = "1.0.0",
                OriginalVersionString = "1.0.0",
                Listed = true,
                Published = DateTime.UtcNow,
                Origin = PackageOrigin.Published,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await TestPackageBuilder.PublishAsync(feed.Client, "Local.Feed.Package", "1.0.0");

        using (var scope = feed.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            context.Packages.Add(new Package
            {
                FeedId = "other-feed",
                Id = "Local.Feed.Package",
                Version = NuGet.Versioning.NuGetVersion.Parse("9.0.0"),
                NormalizedVersionString = "9.0.0",
                OriginalVersionString = "9.0.0",
                Listed = true,
                Published = DateTime.UtcNow.AddDays(1),
                Origin = PackageOrigin.Published,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var search = await feed.Client.GetAsync(
            "/v3/search?q=Package&take=20&prerelease=true",
            TestContext.Current.CancellationToken);
        search.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await search.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var ids = json.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("id").GetString())
            .ToList();

        Assert.Contains("Local.Feed.Package", ids);
        Assert.DoesNotContain("Other.Feed.Package", ids);

        var localPackage = json.RootElement.GetProperty("data")
            .EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == "Local.Feed.Package");
        Assert.Equal("1.0.0", localPackage.GetProperty("version").GetString());
        var versions = localPackage.GetProperty("versions")
            .EnumerateArray()
            .Select(e => e.GetProperty("version").GetString())
            .ToList();
        Assert.DoesNotContain("9.0.0", versions);

        using var packageScope = feed.Services.CreateScope();
        var packages = await packageScope.ServiceProvider
            .GetRequiredService<IPackageService>()
            .FindAsync("Local.Feed.Package", includeUnlisted: false, TestContext.Current.CancellationToken);
        var packageVersions = packages.Select(p => p.Version.ToNormalizedString()).ToList();
        Assert.Contains("1.0.0", packageVersions);
        Assert.DoesNotContain("9.0.0", packageVersions);
    }

    [Fact]
    public async Task OciNamedSegment_UsesSeparateRegistryRoots()
    {
        await using var host = await FeedTestServerHost.StartAsync();
        var client = host.Client;

        var defaultRoot = await client.GetAsync("/v2/");
        var dockerRoot = await client.GetAsync("/docker/v2/");
        var helmEmbedded = await client.GetAsync("/v2/helm/");

        Assert.Equal(HttpStatusCode.OK, defaultRoot.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dockerRoot.StatusCode);
        Assert.Equal(HttpStatusCode.OK, helmEmbedded.StatusCode);
    }

    [Fact]
    public async Task NamedFeedHealth_ReportsRegisteredSurfacesAndRejectsUnknownFeed()
    {
        await using var host = await FeedTestServerHost.StartAsync();

        using var response = await host.Client.GetAsync(
            "/health/feeds/default",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        Assert.Equal("default", document.RootElement.GetProperty("feedId").GetString());
        Assert.Equal("healthy", document.RootElement.GetProperty("status").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("surfaces").EnumerateArray(),
            surface => surface.GetProperty("protocol").GetString() == "NuGet");
        Assert.Contains(
            document.RootElement.GetProperty("surfaces").EnumerateArray(),
            surface => surface.GetProperty("protocol").GetString() == "Npm");
        Assert.Contains(
            document.RootElement.GetProperty("surfaces").EnumerateArray(),
            surface => surface.GetProperty("protocol").GetString() == "Oci");

        using var missing = await host.Client.GetAsync(
            "/health/feeds/unknown",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static IFeedRegistry CreateMultiSurfaceRegistry()
    {
        var feed = new FeedContext("default", "default", "feeds/default/");
        var registry = new FeedRegistry(feed);
        registry.Register(new SurfaceRegistration("nuget", FeedProtocol.NuGet, null, string.Empty, "Feed:NuGet"));
        registry.Register(new SurfaceRegistration("npm", FeedProtocol.Npm, null, "/npm", "Feed:Npm"));
        registry.Register(new SurfaceRegistration("oci", FeedProtocol.Oci, null, string.Empty, "Feed:Oci:Default"));
        registry.Register(new SurfaceRegistration("docker", FeedProtocol.Oci, "docker", "/docker", "Feed:Oci:Docker"));
        return registry;
    }

}
