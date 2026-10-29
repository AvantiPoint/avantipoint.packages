using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Packages.Registry.Native.Extensions;
using AvantiPoint.Packages.Registry.Npm.Extensions;
using AvantiPoint.Packages.Registry.Oci.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class FeedDefaultsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshApplicationsRegisterOnlyNuGetAndHideOptionalNavigation(bool managed)
    {
        await using var openFeed = managed ? null : new NativeUiFactory(enabled: false);
        await using var host = managed ? new ManagedHostFactory(enabled: false) : null;
        using var client = managed ? host!.CreateClient() : openFeed!.CreateClient();
        var services = managed ? host!.Services : openFeed!.Services;
        var registry = services.GetRequiredService<IFeedRegistry>();
        Assert.Equal(FeedProtocol.NuGet, Assert.Single(registry.Surfaces).Protocol);
        var html = await client.GetStringAsync("/", Xunit.TestContext.Current.CancellationToken);
        foreach (var path in new[] { "/npm", "/oci", "/native/maven", "/native/swift", "/native/pub" })
        {
            Assert.DoesNotContain($"href=\"{path}\"", html);
            Assert.Equal(System.Net.HttpStatusCode.NotFound,
                (await client.GetAsync(path, Xunit.TestContext.Current.CancellationToken)).StatusCode);
        }
    }

    [Theory]
    [InlineData("Npm:Enabled", FeedProtocol.Npm, null)]
    [InlineData("Oci:Default:Enabled", FeedProtocol.Oci, null)]
    [InlineData("Oci:Docker:Enabled", FeedProtocol.Oci, "docker")]
    [InlineData("Oci:Helm:Enabled", FeedProtocol.Oci, "helm")]
    [InlineData("Maven:Enabled", FeedProtocol.Maven, null)]
    [InlineData("Swift:Enabled", FeedProtocol.Swift, null)]
    [InlineData("Pub:Enabled", FeedProtocol.Pub, null)]
    public void OptionalConfigurationEnablesOnlyTheSelectedSurface(string key, FeedProtocol protocol, string? segment)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Feed:PublicBaseUrl"] = "https://registry.example.test/",
            ["Feed:" + key] = "true",
        });
        var section = builder.Configuration.GetSection("Feed");
        var feed = builder.AddAvantiPointFeed(section);
        feed.UseNuGet();
        feed.UseNpmRegistryIfEnabled(section.GetSection("Npm"));
        feed.UseConfiguredOciSurfaces(section.GetSection("Oci"));
        feed.UseNativeRegistriesIfEnabled(section);
        Assert.Equal(2, feed.Registry.Surfaces.Count);
        Assert.Contains(feed.Registry.Surfaces, surface => surface.Protocol == FeedProtocol.NuGet);
        Assert.Contains(feed.Registry.Surfaces, surface => surface.Protocol == protocol && surface.OciSegment == segment);
    }
}
