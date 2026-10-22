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
        Assert.Single(cut.FindAll("[role=tabpanel]"));

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
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task LateDiffAfterRapidVersionChangeIsIgnored()
    {
        using var ctx = Context();
        var diffs = new DelayedDiff();
        ctx.Services.AddSingleton<IPackageDiffService>(diffs);
        var cut = ctx.Render<PackageDetail>(p => p.Add(x => x.Package, Package("AppPortal.Cli", "3.0.52")));
        var first = cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.52?tab=diff", false));
        await cut.InvokeAsync(() => cut.Instance.NavigateDetail("/packages/AppPortal.Cli/3.0.60?tab=dependencies", false));
        diffs.Result.SetResult(new PackageDiffResult { PackageId = "AppPortal.Cli", CurrentVersion = "3.0.52", Message = "STALE DIFF" });
        await first;
        Assert.DoesNotContain("STALE DIFF", cut.Markup);
        Assert.Contains("dependency-3.0.60", cut.Markup);
    }

    private static BunitContext Context()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddHttpClient();
        ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        ctx.Services.AddSingleton<IPackageDiffService>(new DelayedDiff());
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
        internal TaskCompletionSource<HttpResponseMessage> First = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return request.RequestUri!.AbsolutePath.Contains("3.0.52") ? First.Task : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("# CURRENT README") });
        }
    }
    private sealed class DelayedDiff : IPackageDiffService
    {
        internal TaskCompletionSource<PackageDiffResult> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<PackageDiffResult> GetDiffAsync(string packageId, string currentVersion, string? compareVersion,
            bool onlyOneVersionInFeed, string? filePath = null, string? view = null, string? tfm = null, CancellationToken cancellationToken = default) => Result.Task;
    }
}
