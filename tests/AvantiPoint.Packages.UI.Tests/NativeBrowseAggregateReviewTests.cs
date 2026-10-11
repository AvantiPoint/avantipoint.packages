using System.Net;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TestContext = Xunit.TestContext;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeBrowseAggregateReviewTests
{
    [Theory]
    [InlineData("pub", "changed-token")]
    [InlineData("swift", "reader")]
    public async Task SuccessfulAuthorizationCannotBeReusedForChangedCredentialsOrAnotherSurface(string protocol, string token)
    {
        var authentication = new FirstValidationOnlyNativeAuthentication();
        await using var factory = new NativeUiFactory(anonymous: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IFeedTokenAuthenticationService>(authentication)));
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Request.Headers.Authorization = "Bearer reader";
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        var browse = scope.ServiceProvider.GetRequiredService<NativePackageBrowseService>();
        Assert.Equal(200, await browse.AuthorizeAsync("pub", TestContext.Current.CancellationToken));
        http.Request.Headers.Authorization = "Bearer " + token;
        Assert.Equal(403, await browse.AuthorizeAsync(protocol, TestContext.Current.CancellationToken));
        Assert.Equal(2, authentication.Calls);
    }

    [Fact]
    public async Task CatalogPagesAreFilledFromVisiblePackagesAcrossDeniedIdentityBatches()
    {
        await using var factory = new NativeUiFactory();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IFeedActionHandler, NativeCatalogVisibilityPolicy>()));
        using var client = app.CreateClient();
        for (var index = 0; index < 50; index++)
            await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", $"hidden_{index:D2}", "1.0.0");
        for (var index = 0; index < 51; index++)
            await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", $"visible_{index:D2}", "1.0.0");
        var first = await client.GetStringAsync("/native/pub", TestContext.Current.CancellationToken);
        Assert.Contains(">visible_00<", first);
        Assert.Contains(">visible_49<", first);
        Assert.DoesNotContain(">visible_50<", first);
        Assert.DoesNotContain(">hidden_", first);
        Assert.Contains("Next", first);
        var second = await client.GetStringAsync("/native/pub?page=2", TestContext.Current.CancellationToken);
        Assert.Contains(">visible_50<", second);
        Assert.DoesNotContain(">visible_49<", second);
        Assert.DoesNotContain("Next", second);
    }

    [Theory]
    [InlineData(false, "/native/pub")]
    [InlineData(false, "/native/pub/feed")]
    [InlineData(false, "/native/pub/packages/sdk")]
    [InlineData(true, "/native/pub")]
    [InlineData(true, "/native/pub/feed")]
    [InlineData(true, "/native/pub/packages/sdk")]
    public async Task PageValidatesTokenOnceAndDoesNotCarryAuthorizationIntoAnotherRequest(bool managed, string path)
    {
        var authentication = new FirstValidationOnlyNativeAuthentication();
        if (managed)
        {
            await using var factory = new ManagedHostFactory(anonymous: false);
            await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton<IFeedTokenAuthenticationService>(authentication)));
            using var client = app.CreateClient(new() { AllowAutoRedirect = false });
            await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", "sdk", "1.0.0");
            await AssertOneValidationPerRequest(client, path, authentication);
        }
        else
        {
            await using var factory = new NativeUiFactory(anonymous: false);
            await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton<IFeedTokenAuthenticationService>(authentication)));
            using var client = app.CreateClient();
            await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", "sdk", "1.0.0");
            await AssertOneValidationPerRequest(client, path, authentication);
        }
    }

    private static async Task AssertOneValidationPerRequest(HttpClient client, string path,
        FirstValidationOnlyNativeAuthentication authentication)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", "reader");
        using var first = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, authentication.Calls);
        using var second = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        Assert.Equal(2, authentication.Calls);
    }
}
