using System.Net;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Registry.Npm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeHostStartupTests
{
    [Fact]
    public async Task ManagedHeaderRetainsRegisteredNpmSetupInstructions()
    {
        await using var factory = new ManagedHostFactory(enabled: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var registry = (IFeedRegistry)services.Single(descriptor => descriptor.ServiceType == typeof(IFeedRegistry))
                .ImplementationInstance!;
            registry.Register(new("npm", FeedProtocol.Npm, null, "/npm", "Feed:Npm"));
            services.AddNpmRegistry();
        }));
        using var client = app.CreateClient();
        var html = await client.GetStringAsync("/", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("href=\"/npm/feed\"", html);
        var guide = await client.GetStringAsync("/npm/feed", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("npm config set registry", guide);
    }

    [Fact]
    public async Task DevelopmentHostWithDisabledRegistriesStartsAndReturns404WithoutMissingServices()
    {
        await using var factory = new ManagedHostFactory(enabled: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = app.CreateClient();
        foreach (var path in new[] { "/native/maven", "/native/swift", "/native/pub" })
        {
            using var response = await client.GetAsync(path, Xunit.TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var home = await client.GetAsync("/", Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        var html = await home.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("Package Feed</a>", html);
        Assert.Contains("href=\"/feed\"", html);
    }
}
