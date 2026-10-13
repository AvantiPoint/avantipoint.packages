using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeHostStartupTests
{
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
    }
}
