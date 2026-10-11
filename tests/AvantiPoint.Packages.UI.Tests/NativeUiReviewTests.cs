using System.Net;
using System.Security.Claims;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Hosting;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeUiReviewTests
{
    [Theory]
    [InlineData("PackagePublisher", 403)]
    [InlineData("PackageConsumer", 200)]
    public async Task PrivateManagedMetadataRequiresConsumerRole(string role, int expected)
    {
        await using var factory = new ManagedHostFactory(anonymous: false);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Cookies"));
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        Assert.Equal(expected, await scope.ServiceProvider.GetRequiredService<NativePackageBrowseService>().AuthorizeAsync("pub"));
    }

    [Fact]
    public async Task ManagedNativePagesValidateTokensBeforeTheBrowserPolicy()
    {
        await using var factory = new ManagedHostFactory(anonymous: false);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddAuthorization(options => options.AddPolicy("UI", policy => policy.RequireAuthenticatedUser()));
            services.Configure<RazorPagesOptions>(options => options.Conventions.AuthorizeFolder("/", "UI"));
            services.AddSingleton<AvantiPoint.Feed.Platform.Authentication.IFeedTokenAuthenticationService, NativeUiTokenAuthentication>();
        }));
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new("Bearer", "reader");
        using var response = await client.GetAsync("/native/pub");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "writer");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/native/pub")).StatusCode);
    }

    [Fact]
    public async Task SearchAndDetailsAuthorizeOnlyMatchingPackagesAndBrowseIsPaged()
    {
        var handler = new CountingNativeUiHandler();
        await using var factory = new NativeUiFactory();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IFeedActionHandler>(handler)));
        using var client = app.CreateClient();
        for (var i = 0; i < 52; i++)
            await NativeUiTestArtifacts.SeedAsync(app.Services, "Pub", $"sdk_{i:D2}", "1.0.0");
        await client.GetStringAsync("/native/pub/packages/sdk_00");
        Assert.Equal(1, handler.Calls);
        handler.Calls = 0;
        await client.GetStringAsync("/native/pub?q=SDK_01");
        Assert.Equal(1, handler.Calls);
        handler.Calls = 0;
        var first = await client.GetStringAsync("/native/pub");
        Assert.True(handler.Calls <= 50);
        Assert.DoesNotContain(">sdk_51<", first);
        Assert.Contains("Next", first);
        var second = await client.GetStringAsync("/native/pub?page=2");
        Assert.Contains(">sdk_51<", second);
    }
}
