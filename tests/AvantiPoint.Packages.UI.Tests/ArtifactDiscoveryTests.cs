using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.UI;
using AvantiPoint.Packages.UI.Components.MultiFeed;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class ArtifactDiscoveryTests
{
    [Fact]
    public async Task Service_SearchesEveryConfiguredSurfaceAndBuildsNativeRoutes()
    {
        var registry = new FeedRegistry(new FeedContext("team", "Team", "feeds/team/"));
        registry.Register(new SurfaceRegistration("nuget", FeedProtocol.NuGet, null, string.Empty, "Feed:NuGet"));
        registry.Register(new SurfaceRegistration("npm", FeedProtocol.Npm, null, "/npm", "Feed:Npm"));
        registry.Register(new SurfaceRegistration("docker", FeedProtocol.Oci, "docker", "/docker", "Feed:Oci:Docker"));
        registry.Register(new SurfaceRegistration("helm", FeedProtocol.Oci, "helm", "/helm", "Feed:Oci:Helm"));

        var nuget = new RecordingNuGetSearchService();
        var npm = new RecordingNpmBrowseService();
        var oci = new RecordingOciBrowseService();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFeedRegistry>(registry);
        services.AddSingleton<INuGetSearchService>(nuget);
        services.AddSingleton<INpmPackageBrowseService>(npm);
        services.AddSingleton<IOciRepositoryBrowseService>(oci);
        services.AddOptions<OciFeedOptions>(OciSurfaceOptionsBuilder.GetOptionsName("docker"))
            .Configure(options => options.Profile = OciProfile.ContainerImages);
        services.AddOptions<OciFeedOptions>(OciSurfaceOptionsBuilder.GetOptionsName("helm"))
            .Configure(options => options.Profile = OciProfile.HelmCharts);
        services.AddArtifactDiscoveryUi();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IArtifactDiscoveryService>()
            .SearchAsync(
                " platform ",
                takePerSurface: 3,
                cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("platform", result.Query);
        Assert.Equal(["NuGet", "npm", "Docker", "Helm"], result.Sections.Select(section => section.Name));
        Assert.All([nuget.LastQuery, npm.LastQuery, .. oci.Queries], query => Assert.Equal("platform", query));

        var nugetItem = Assert.Single(result.Sections[0].Items);
        Assert.Equal("/packages/Platform.Core/2.0.0", nugetItem.Href);

        var npmItem = Assert.Single(result.Sections[1].Items);
        Assert.Equal("/browse/npm/packages/%40avantipoint/platform", npmItem.Href);

        var docker = result.Sections[2];
        Assert.Equal(ArtifactDiscoveryKind.Docker, docker.Kind);
        Assert.Equal("/oci/docker/repos/team/platform", Assert.Single(docker.Items).Href);

        var helm = result.Sections[3];
        Assert.Equal(ArtifactDiscoveryKind.Helm, helm.Kind);
        Assert.Equal("/oci/helm/repos/charts/platform", Assert.Single(helm.Items).Href);
    }

    [Fact]
    public void Component_RendersOneSearchWithGroupedNativeResultsAndPreservesQuery()
    {
        using var context = new BunitContext();
        context.Services.AddLogging();
        var service = new StaticDiscoveryService();
        context.Services.AddSingleton<IArtifactDiscoveryService>(service);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("browse?q=platform%20api");

        var component = context.Render<ArtifactDiscovery>();

        component.WaitForAssertion(() =>
        {
            Assert.Equal("platform api", service.LastQuery);
            Assert.Equal("Find the dependency or deployment artifact you need", component.Find("h1").TextContent);
            Assert.Equal("platform api", component.Find("input[type=search]").GetAttribute("value"));
            Assert.Equal(2, component.FindAll("section").Count);
            Assert.Contains("Platform.Core", component.Markup, StringComparison.Ordinal);
            Assert.Contains("@avantipoint/platform", component.Markup, StringComparison.Ordinal);
            Assert.Contains(
                component.FindAll("a"),
                link => link.TextContent.Contains("Browse all", StringComparison.Ordinal)
                    && link.GetAttribute("href") == "/browse/npm?q=platform%20api");
        });
    }

    [Fact]
    public void Component_IsolatesUnavailableSurfaceWithoutHidingHealthyResults()
    {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddSingleton<IArtifactDiscoveryService>(new PartialFailureDiscoveryService());

        var component = context.Render<ArtifactDiscovery>();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("NuGet results are temporarily unavailable", component.Markup, StringComparison.Ordinal);
            Assert.Contains("@avantipoint/platform", component.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(component.FindAll("[role=alert]"), alert => alert.TextContent.Contains("couldn't search", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void UnifiedRoute_DoesNotShowActionsForAStaleProtocolCookie()
    {
        using var context = new BunitContext();
        var registry = new FeedRegistry(new FeedContext("team", "Team", "feeds/team/"));
        registry.Register(new SurfaceRegistration("nuget", FeedProtocol.NuGet, null, string.Empty, "Feed:NuGet"));
        registry.Register(new SurfaceRegistration("npm", FeedProtocol.Npm, null, "/npm", "Feed:Npm"));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{FeedProtocolPreference.CookieName}=npm";
        context.Services.AddSingleton<IFeedRegistry>(registry);
        context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        context.Services.AddOptions();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("browse");

        var component = context.Render<FeedUiNavigation>(parameters => parameters
            .Add(parameter => parameter.IncludeBrowseLinks, false));

        var link = Assert.Single(component.FindAll("a"));
        Assert.Equal("Explore", link.TextContent.Trim());
        Assert.Equal("/browse", link.GetAttribute("href"));
        Assert.DoesNotContain("Connect", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Search<", component.Markup, StringComparison.Ordinal);
    }

    private sealed class RecordingNuGetSearchService : INuGetSearchService
    {
        public string? LastQuery { get; private set; }

        public Task<SearchResponse> SearchAsync(
            string? query = null,
            int skip = 0,
            int take = 20,
            bool includePrerelease = false,
            string? packageType = null,
            string? framework = null,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new SearchResponse
            {
                TotalHits = 1,
                Data =
                [
                    new SearchResult
                    {
                        PackageId = "Platform.Core",
                        Version = "2.0.0",
                        Description = "Shared platform primitives",
                        Authors = ["Platform Team"],
                        Published = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
                    },
                ],
            });
        }

        public Task<IReadOnlyList<string>> AutocompleteAsync(
            string? query = null,
            int skip = 0,
            int take = 10,
            bool includePrerelease = false,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class RecordingNpmBrowseService : INpmPackageBrowseService
    {
        public string? LastQuery { get; private set; }

        public Task<IReadOnlyList<NpmPackageListItem>> SearchAsync(
            string? query,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<NpmPackageListItem>>
            ([new("@avantipoint/platform", "3.0.0", DateTime.Parse("2026-09-02T00:00:00Z"), "Platform SDK")]);
        }

        public Task<NpmPackageDetailModel?> GetPackageAsync(
            string packageName,
            string? detailedVersion = null,
            CancellationToken cancellationToken = default) => Task.FromResult<NpmPackageDetailModel?>(null);
    }

    private sealed class RecordingOciBrowseService : IOciRepositoryBrowseService
    {
        public List<string?> Queries { get; } = [];

        public Task<OciRepositorySearchResult> SearchRepositoriesAsync(
            string? segment,
            string? query = null,
            string? kind = null,
            int skip = 0,
            int take = 24,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            var isHelm = segment == "helm";
            IReadOnlyList<OciRepositoryListItem> items =
            [
                new(
                    isHelm ? "charts/platform" : "team/platform",
                    isHelm ? OciArtifactKind.Helm : OciArtifactKind.Image,
                    isHelm ? "1.2.0" : "latest",
                    TagCount: 2),
            ];
            return Task.FromResult(new OciRepositorySearchResult(items, 1, 0, take));
        }

        public Task<IReadOnlyList<OciRepositoryListItem>> ListRepositoriesAsync(string? segment, int? max = null, string? last = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OciRepositoryListItem>>([]);

        public Task<OciRepositoryTagsModel?> ListTagsAsync(string repositoryName, string? segment, int? max = null, string? last = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<OciRepositoryTagsModel?>(null);

        public Task<OciArtifactDetailModel?> GetArtifactAsync(string repositoryName, string reference, string? segment, CancellationToken cancellationToken = default) =>
            Task.FromResult<OciArtifactDetailModel?>(null);

        public string GetRegistryRootUrl(string? segment) => "/v2/";

        public string GetRegistryApiUrl(string? segment) => "/v2/";
    }

    private sealed class StaticDiscoveryService : IArtifactDiscoveryService
    {
        public string? LastQuery { get; private set; }

        public Task<ArtifactDiscoveryResult> SearchAsync(string? query, int takePerSurface = 6, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new ArtifactDiscoveryResult(
                query ?? string.Empty,
                [HealthyNuGetSection(), HealthyNpmSection()]));
        }
    }

    private sealed class PartialFailureDiscoveryService : IArtifactDiscoveryService
    {
        public Task<ArtifactDiscoveryResult> SearchAsync(string? query, int takePerSurface = 6, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArtifactDiscoveryResult(
                query ?? string.Empty,
                [
                    HealthyNuGetSection() with
                    {
                        Items = [],
                        ErrorMessage = "NuGet results are temporarily unavailable.",
                    },
                    HealthyNpmSection(),
                ]));
    }

    private static ArtifactDiscoverySection HealthyNuGetSection() => new(
        "nuget",
        "NuGet",
        ArtifactDiscoveryKind.NuGet,
        ".NET packages",
        "/",
        "/feed",
        1,
        false,
        [new("Platform.Core", "2.0.0", "Shared primitives", "/packages/Platform.Core/2.0.0")]);

    private static ArtifactDiscoverySection HealthyNpmSection() => new(
        "npm",
        "npm",
        ArtifactDiscoveryKind.Npm,
        "JavaScript packages",
        "/browse/npm",
        "/browse/npm/connect",
        1,
        false,
        [new("@avantipoint/platform", "3.0.0", "Platform SDK", "/browse/npm/packages/%40avantipoint/platform")]);
}
