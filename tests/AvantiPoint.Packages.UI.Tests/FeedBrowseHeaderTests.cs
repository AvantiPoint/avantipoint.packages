using AvantiPoint.Packages.UI.Components.MultiFeed;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class FeedBrowseHeaderTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void Header_RendersEcosystemContextAndPathBaseSafeConnectionLink()
    {
        _context.Services.AddSingleton<NavigationManager>(new ScopedNavigationManager(
            "https://packages.example.test/packages/",
            "https://packages.example.test/packages/browse/npm"));

        var component = _context.Render<FeedBrowseHeader>(parameters => parameters
            .Add(p => p.Ecosystem, "npm")
            .Add(p => p.Title, "npm packages")
            .Add(p => p.Description, "Find JavaScript packages.")
            .Add(p => p.ConnectHref, "/browse/npm/connect")
            .Add(p => p.ConnectLabel, "Connect npm"));

        Assert.Equal("npm packages", component.Find("h1").TextContent);
        Assert.Contains("Find JavaScript packages.", component.Markup, StringComparison.Ordinal);

        var connectLink = component.Find("a");
        Assert.Equal("/packages/browse/npm/connect", connectLink.GetAttribute("href"));
        Assert.Equal("Connect npm", connectLink.TextContent.Trim().TrimEnd('→').Trim());
        Assert.Equal("true", connectLink.QuerySelector("[aria-hidden]")?.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Header_DoesNotRepeatAnEcosystemThatMatchesTheSurfaceName()
    {
        var component = _context.Render<FeedBrowseHeader>(parameters => parameters
            .Add(p => p.Ecosystem, "Docker")
            .Add(p => p.Title, "Docker")
            .Add(p => p.ConnectHref, "/oci/connect/docker"));

        Assert.Equal("Docker", component.Find("h1").TextContent);
        Assert.DoesNotContain(
            component.FindAll("p"),
            paragraph => paragraph.TextContent.Equals("Docker", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _context.Dispose();

    private sealed class ScopedNavigationManager : NavigationManager
    {
        public ScopedNavigationManager(string baseUri, string uri) => Initialize(baseUri, uri);

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            throw new NotSupportedException();
    }
}
