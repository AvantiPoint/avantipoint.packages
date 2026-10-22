using System.Security.Claims;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.UI.Components.Npm;
using AvantiPoint.Packages.UI.Components.Oci;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class FeedConnectionCopyTests
{
    [Fact]
    public void NuGetConnection_CopyButtonWritesPathBaseAwareFeedUrl()
    {
        using var context = CreateContext(new SurfaceRegistration(
            "nuget",
            FeedProtocol.NuGet,
            OciSegment: null,
            RoutePrefix: string.Empty,
            OptionsSectionKey: "Feed:NuGet"));
        const string expected = "https://packages.example.test/feeds/team-a/v3/index.json";
        context.JSInterop
            .SetupVoid("navigator.clipboard.writeText", expected)
            .SetVoidResult();

        var component = context.Render<FeedInfo>(parameters => parameters
            .Add(parameter => parameter.FeedName, "TeamFeed")
            .Add(parameter => parameter.ShowAuthenticationInfo, true)
            .Add(parameter => parameter.Username, "developer@example.test"));

        AssertCopyControls(component, expectedSnippetCount: 2);
        Assert.DoesNotContain("Copy .NET CLI source command", component.Markup);
        Assert.Contains("--username", component.Markup);
        Assert.Contains($"dotnet nuget add source {expected}", component.Markup, StringComparison.Ordinal);

        component.Find("button[aria-label='Copy NuGet feed URL']").Click();

        var invocation = context.JSInterop.VerifyInvoke("navigator.clipboard.writeText");
        Assert.Equal(expected, Assert.Single(invocation.Arguments));
    }

    [Fact]
    public void NpmConnection_CopyButtonWritesPathBaseAwareAuthSetting()
    {
        using var context = CreateContext(new SurfaceRegistration(
            "npm",
            FeedProtocol.Npm,
            OciSegment: null,
            RoutePrefix: "/npm",
            OptionsSectionKey: "Feed:Npm"));
        const string expected = "registry=https://packages.example.test/feeds/team-a/npm/\n//packages.example.test/feeds/team-a/npm/:_authToken=<API_KEY>";
        context.JSInterop
            .SetupVoid("navigator.clipboard.writeText", expected)
            .SetVoidResult();

        var component = context.Render<NpmFeedInfo>(parameters => parameters
            .Add(parameter => parameter.ShowAuthenticationInfo, true));

        AssertCopyControls(component, expectedSnippetCount: 2);
        Assert.DoesNotContain("Copy npm registry command", component.Markup);
        Assert.Contains("https://packages.example.test/feeds/team-a/npm/", component.Markup, StringComparison.Ordinal);

        component.Find("button[aria-label='Copy authenticated .npmrc configuration']").Click();

        var invocation = context.JSInterop.VerifyInvoke("navigator.clipboard.writeText");
        Assert.Equal(expected, Assert.Single(invocation.Arguments));
    }

    [Fact]
    public void OciConnection_CopyButtonsCoverEveryUrlAndCommand()
    {
        using var context = CreateContext(new SurfaceRegistration(
            "oci-docker",
            FeedProtocol.Oci,
            OciSegment: "docker",
            RoutePrefix: "/docker",
            OptionsSectionKey: "Feed:Oci:Docker"));
        context.Services.AddSingleton<IOciRepositoryBrowseService>(
            new StubOciRepositoryBrowseService("https://packages.example.test/feeds/team-a/docker/v2/"));
        context.Services.AddOptions<OciFeedOptions>(OciSurfaceOptionsBuilder.GetOptionsName("docker"))
            .Configure(options => options.Profile = OciProfile.General);
        const string expected = "helm pull oci://packages.example.test/docker/{repository} --version {version}";
        context.JSInterop
            .SetupVoid("navigator.clipboard.writeText", expected)
            .SetVoidResult();

        var component = context.Render<OciFeedInfo>(parameters => parameters
            .Add(parameter => parameter.Segment, "docker")
            .Add(parameter => parameter.ShowAuthenticationInfo, true));

        AssertCopyControls(component, expectedSnippetCount: 7);
        Assert.DoesNotContain("REGISTRY_TOKEN", component.Markup);
        Assert.Contains("https://packages.example.test/feeds/team-a/docker/v2/", component.Markup, StringComparison.Ordinal);

        component.Find("button[aria-label='Copy Helm pull command']").Click();

        var invocation = context.JSInterop.VerifyInvoke("navigator.clipboard.writeText");
        Assert.Equal(expected, Assert.Single(invocation.Arguments));
    }

    private static BunitContext CreateContext(SurfaceRegistration surface)
    {
        var context = new BunitContext();
        var registry = new FeedRegistry(new FeedContext("team-a", "Team A", "feeds/team-a/"));
        registry.Register(surface);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("packages.example.test");
        httpContext.Request.PathBase = "/feeds/team-a";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, "developer@example.test")],
            authenticationType: "test"));

        context.Services.AddSingleton<IFeedRegistry>(registry);
        context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = httpContext,
        });
        context.Services.AddOptions<FeedOptions>();
        context.Services.AddOptions<PackageFeedOptions>();
        context.Services.AddSingleton<IPublicBaseUrlProvider, PublicBaseUrlProvider>();
        return context;
    }

    private static void AssertCopyControls<TComponent>(
        IRenderedComponent<TComponent> component,
        int expectedSnippetCount)
        where TComponent : IComponent
    {
        var snippets = component.FindAll(".install-snippet");
        var buttons = component.FindAll("button.install-snippet__copy");

        Assert.Equal(expectedSnippetCount, snippets.Count);
        Assert.Equal(snippets.Count, buttons.Count);
        Assert.All(buttons, button =>
        {
            Assert.Equal("button", button.GetAttribute("type"));
            Assert.StartsWith("Copy ", button.GetAttribute("aria-label"), StringComparison.Ordinal);
        });
    }

    private sealed class StubOciRepositoryBrowseService(string registryApiUrl) : IOciRepositoryBrowseService
    {
        public Task<OciRepositorySearchResult> SearchRepositoriesAsync(
            string? segment,
            string? query = null,
            string? kind = null,
            int skip = 0,
            int take = 24,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<OciRepositoryListItem>> ListRepositoriesAsync(
            string? segment,
            int? max = null,
            string? last = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OciRepositoryTagsModel?> ListTagsAsync(
            string repositoryName,
            string? segment,
            int? max = null,
            string? last = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OciArtifactDetailModel?> GetArtifactAsync(
            string repositoryName,
            string reference,
            string? segment,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string GetRegistryRootUrl(string? segment) => registryApiUrl[..^3];

        public string GetRegistryApiUrl(string? segment) => registryApiUrl;
    }
}
