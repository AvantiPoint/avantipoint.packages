using System.Net;
using Microsoft.Extensions.Configuration;
using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class PackageDetailNavigationTests
{
    [Fact]
    public async Task VersionAndTabNavigationUsesLoadedMetadataAndDoesNotMutateInput()
    {
        using var ctx = Context();
        var package = Package("AppPortal.Cli", "3.0.52");
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, package));
        Assert.Equal("3.0.52", cut.Find("select").GetAttribute("value"));
        Assert.Single(cut.FindAll("div[role=tabpanel]"));

        await cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.60?tab=dependencies", false));
        Assert.Contains("dependency-3.0.60", cut.Markup);
        Assert.DoesNotContain("dependency-3.0.52", cut.Markup);
        Assert.Equal("3.0.52", package.Version.OriginalVersion);
        Assert.Equal("3.0.60", cut.Find("select").GetAttribute("value"));

        cut.Render(p => p.Add(x => x.Package, Package("Prism.Core", "3.0.52")));
        Assert.Equal("3.0.52", cut.Find("select").GetAttribute("value"));
        Assert.Contains("Prism.Core", cut.Markup);
    }

    [Fact]
    public async Task LateReadmeFromPreviousVersionNeverReplacesCurrentVersion()
    {
        using var ctx = Context();
        var handler = new DelayedReadmes();
        ctx.Services.AddSingleton<IHttpClientFactory>(new ClientFactory(handler));
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52", readme: true)));
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Calls));
        await cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.60?tab=readme", false));
        handler.First.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("# OLD README") });
        cut.WaitForAssertion(() => Assert.Contains("CURRENT README", cut.Markup));
        Assert.DoesNotContain("OLD README", cut.Markup);
        await cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.60?tab=dependencies", false));
        await cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.60?tab=readme", false));
        Assert.Contains("CURRENT README", cut.Markup);
        Assert.Single(handler.Paths, path => path.Contains("3.0.60", StringComparison.Ordinal));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public void ExplicitUnlistedNormalizedVersionRendersItsOwnMetadata()
    {
        using var ctx = Context();
        var package = Package("AppPortal.Cli", "3.0.52");
        package.Versions = package.Versions.Select(v => v.Version.OriginalVersion == "3.0.60" ? v with { IsListed = false } : v).ToList();
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, package).Add(x => x.RequestedVersion, "3.0.60.0"));
        Assert.Contains("Description 3.0.60", cut.Markup);
        Assert.Equal("3.0.60", cut.Find("select").GetAttribute("value"));
        Assert.Contains("AppPortal.Cli", cut.Markup);
        Assert.DoesNotContain("Description 3.0.52", cut.Markup);
    }

    [Fact]
    public void MissingRequestedVersionDoesNotShowAnotherVersionsMetadataOrInstallCommand()
    {
        using var ctx = Context();
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52"))
            .Add(x => x.RequestedVersion, "99.0.0"));
        Assert.Contains("Version 99.0.0 is not available", cut.Markup);
        Assert.DoesNotContain("Description 3.0.52", cut.Markup);
        Assert.DoesNotContain("dotnet add package", cut.Markup);
        Assert.Empty(cut.FindAll("select"));
    }

    [Fact]
    public async Task CustomLicenseTextIsEncodedAndCanBeReopenedWithoutAnotherRead()
    {
        using var ctx = Context();
        var handler = new LicenseHandler();
        ctx.Services.AddSingleton<IHttpClientFactory>(new ClientFactory(handler));
        var package = Package("AppPortal.Cli", "3.0.52");
        package.Versions = package.Versions.Select(v => v with { HasEmbeddedLicense = true, LicenseExpression = "AvantiPoint-Custom" }).ToList();
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, package));
        await cut.Find("button[aria-haspopup=dialog]").ClickAsync(new());
        Assert.Contains("<script>unsafe()</script>", cut.Find(".package-license-text").TextContent);
        Assert.DoesNotContain("<script>", cut.Markup);
        Assert.DoesNotContain("spdx.org/licenses/AvantiPoint", cut.Markup);
        await cut.Find("button[aria-haspopup=dialog]").ClickAsync(new());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task AuthenticationChangeClearsPreviouslyRenderedText()
    {
        using var ctx = Context();
        var auth = new ChangingAuthentication();
        ctx.Services.AddSingleton<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(auth);
        ctx.Services.AddSingleton<IHttpClientFactory>(new ClientFactory(new LicenseHandler()));
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52", readme: true)));
        cut.WaitForAssertion(() => Assert.Contains("Custom terms", cut.Markup));
        await cut.InvokeAsync(auth.SignOut);
        Assert.DoesNotContain("Custom terms", cut.Markup);
    }

    [Fact]
    public async Task PrerenderShowsPendingReadmeInsteadOfClaimingNoReadme()
    {
        using var ctx = Context();
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(ctx.Services,
            ctx.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<PackageDetail>(Microsoft.AspNetCore.Components.ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Package"] = Package("AppPortal.Cli", "3.0.52", readme: true) }));
            return rendered.ToHtmlString();
        });
        Assert.Contains("Loading ReadMe", html);
        Assert.DoesNotContain("No ReadMe available", html);
    }

    [Fact]
    public async Task ExplicitSelectionPreservesTheExistingVersionCallbackContract()
    {
        using var ctx = Context();
        var selected = new List<NuGetVersion>();
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52"))
            .Add(x => x.OnVersionSelected, (NuGetVersion value) => selected.Add(value)));
        await cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.60?tab=versions", true));
        Assert.Equal("3.0.60", Assert.Single(selected).OriginalVersion);
        Assert.Contains("3.0.60", cut.Find("select").GetAttribute("value"));
    }

    [Fact]
    public async Task UnchangedParentParametersPreserveInternallySelectedContentAndInstallTabs()
    {
        using var ctx = Context();
        var parent = ctx.Render<RerenderingParent>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52")));
        var detail = parent.FindComponent<PackageDetail>();
        await detail.InvokeAsync(() => detail.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.52?tab=versions&install=cpm", true));
        await parent.Instance.RerenderAsync();
        AssertSelectedTabs(detail, "Versions", "Central Package Management");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VersionCallbackParentRerenderPreservesSelectedTabs(bool updateRequestedVersion)
    {
        using var ctx = Context();
        var parent = ctx.Render<RerenderingParent>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52"))
            .Add(x => x.UpdateRequestedVersion, updateRequestedVersion));
        var detail = parent.FindComponent<PackageDetail>();
        await detail.InvokeAsync(() => detail.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.60?tab=versions&install=cpm", true));
        Assert.Equal("3.0.60", Assert.Single(parent.Instance.SelectedVersions).OriginalVersion);
        Assert.Equal("3.0.60", detail.Find("select").GetAttribute("value"));
        AssertSelectedTabs(detail, "Versions", "Central Package Management");
    }

    [Fact]
    public async Task ChangedNavigationParametersApplyIndependentlyAndNewPackagesUseIncomingTabs()
    {
        using var ctx = Context();
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52"))
            .Add(x => x.Tab, "readme").Add(x => x.Install, "cli"));
        await cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.52?tab=versions&install=cpm", false));
        cut.Render(p => p.Add(x => x.Tab, "dependencies"));
        AssertSelectedTabs(cut, "References and Dependencies", "Central Package Management");
        cut.Render(p => p.Add(x => x.Install, "pmc"));
        AssertSelectedTabs(cut, "References and Dependencies", "Package Manager");
        await cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.52?tab=versions&install=cpm", false));
        cut.Render(p => p.Add(x => x.Package, Package("Prism.Core", "3.0.52")));
        AssertSelectedTabs(cut, "References and Dependencies", "Package Manager");
    }

    private static void AssertSelectedTabs(IRenderedComponent<PackageDetail> detail, string content, string install)
    {
        Assert.Equal(content, detail.Find(".package-tabs [aria-selected=true]").TextContent.Trim());
        Assert.Equal(install, detail.Find(".install-tabs [aria-selected=true]").TextContent.Trim());
    }

    public sealed class RerenderingParent : Microsoft.AspNetCore.Components.ComponentBase
    {
        [Microsoft.AspNetCore.Components.Parameter]
        public PackageInfoCollection Package { get; set; } = default!;
        [Microsoft.AspNetCore.Components.Parameter]
        public bool UpdateRequestedVersion { get; set; }
        public List<NuGetVersion> SelectedVersions { get; } = [];
        private string requestedVersion = "3.0.52";

        public Task RerenderAsync() => InvokeAsync(StateHasChanged);

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<PackageDetail>(0);
            builder.AddAttribute(1, nameof(PackageDetail.Package), Package);
            builder.AddAttribute(2, nameof(PackageDetail.RequestedVersion), requestedVersion);
            builder.AddAttribute(3, nameof(PackageDetail.Tab), "readme");
            builder.AddAttribute(4, nameof(PackageDetail.Install), "cli");
            builder.AddAttribute(5, nameof(PackageDetail.OnVersionSelected),
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NuGetVersion>(this, (NuGetVersion version) =>
                {
                    SelectedVersions.Add(version);
                    if (UpdateRequestedVersion) requestedVersion = version.OriginalVersion;
                }));
            builder.CloseComponent();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InProcessTextIsUsedOnlyForTheCurrentSignedInContext(bool signedIn)
    {
        using var ctx = Context();
        var auth = new ChangingAuthentication(signedIn);
        var content = System.Reflection.DispatchProxy.Create<AvantiPoint.Packages.Core.IPackageContentService, ContentProxy>();
        ctx.Services.AddSingleton<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(auth);
        ctx.Services.AddSingleton(content);
        ctx.Services.AddSingleton<IHttpClientFactory>(new ClientFactory(new LicenseHandler()));
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52", readme: true)));
        cut.WaitForAssertion(() => Assert.Contains(signedIn ? "Signed-in text" : "Custom terms", cut.Markup));
        Assert.Equal(signedIn ? 1 : 0, ((ContentProxy)(object)content).Calls);
    }

    public class ContentProxy : System.Reflection.DispatchProxy
    {
        internal int Calls;
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args)
        {
            Calls++;
            return Task.FromResult<Stream?>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("# Signed-in text")));
        }
    }

    [Fact]
    public async Task ClearingThePackageDuringNavigationRejectsLateContentAndClearsMetadata()
    {
        using var ctx = Context();
        var handler = new DelayedReadmes();
        ctx.Services.AddSingleton<IHttpClientFactory>(new ClientFactory(handler));
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52", readme: true)));
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Calls));
        await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.Package, null!)));
        handler.First.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("# OLD PRIVATE README") });
        cut.WaitForAssertion(() => Assert.Contains("No package metadata available", cut.Markup));
        Assert.DoesNotContain("AppPortal.Cli", cut.Markup);
        Assert.DoesNotContain("OLD PRIVATE README", cut.Markup);
    }

    private sealed class ChangingAuthentication(bool signedIn = false) : Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider
    {
        public override Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(new System.Security.Claims.ClaimsPrincipal(signedIn
                ? new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("name", "synthetic")], "test")
                : new System.Security.Claims.ClaimsIdentity())));
        internal void SignOut() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private sealed class LicenseHandler : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Custom terms <script>unsafe()</script>") });
        }
    }

    private static BunitContext Context()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddHttpClient();
        ctx.Services.AddSingleton<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(new ChangingAuthentication());
        ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return ctx;
    }

    private static PackageInfoCollection Package(string id, string version, bool readme = false) => new(version)
    {
        Versions = new[] { "3.0.52", "3.0.60" }.Select(v => new PackageInfo
        {
            PackageId = id, Version = NuGetVersion.Parse(v), IsListed = true, HasReadme = readme,
            Authors = ["Brian", "Dan Siegel"], Description = $"Description {v}", Published = DateTimeOffset.UtcNow,
            Dependencies = new() { ["net10.0"] = [new() { PackageId = $"dependency-{v}", VersionRange = "[1.0.0]" }] }
        }).ToList()
    };

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class DelayedReadmes : HttpMessageHandler
    {
        internal int Calls;
        internal readonly System.Collections.Concurrent.ConcurrentQueue<string> Paths = new();
        internal TaskCompletionSource<HttpResponseMessage> First = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            Paths.Enqueue(request.RequestUri!.AbsolutePath);
            return request.RequestUri!.AbsolutePath.Contains("3.0.52") ? First.Task : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("# CURRENT README") });
        }
    }
}
