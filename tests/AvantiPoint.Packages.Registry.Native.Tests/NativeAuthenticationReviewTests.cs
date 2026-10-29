using System.Net;
using System.Security.Claims;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Registry.Native.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeAuthenticationReviewTests
{
    [Theory]
    [InlineData(FeedProtocol.Maven)]
    [InlineData(FeedProtocol.Swift)]
    [InlineData(FeedProtocol.Pub)]
    public async Task SuppliedTokenPrincipalReplacesCookiePrincipal(FeedProtocol protocol)
    {
        var token = new PrincipalTokenAuthentication();
        await using var host = await NativeTestHost.StartAsync(tokenAuthentication: token);
        using var scope = host.Services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Request.Headers.Authorization = "Bearer reader";
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "privileged-cookie")], "Cookies"));
        scope.ServiceProvider.GetRequiredService<ISurfaceContextAccessor>().Current =
            new("native-tests", protocol, protocol.ToString().ToLowerInvariant(), null, "/" + protocol, new Uri("https://registry.test/"));
        var filter = new NativeAuthorizationFilter(protocol, FeedOperation.Pull);
        await filter.InvokeAsync(EndpointFilterInvocationContext.Create(http), context =>
        {
            Assert.Same(token.User, context.HttpContext.User);
            Assert.True(context.HttpContext.User.Identity?.IsAuthenticated);
            Assert.False(context.HttpContext.User.IsInRole("privileged-cookie"));
            return ValueTask.FromResult<object?>(Results.Ok());
        });
    }

    [Theory]
    [InlineData(FeedProtocol.Maven)]
    [InlineData(FeedProtocol.Swift)]
    [InlineData(FeedProtocol.Pub)]
    public async Task IdentitylessTokenCannotInheritCookiePrivileges(FeedProtocol protocol)
    {
        await using var host = await NativeTestHost.StartAsync();
        using var scope = host.Services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Request.Headers.Authorization = "Bearer reader";
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "privileged-cookie")], "Cookies"));
        scope.ServiceProvider.GetRequiredService<ISurfaceContextAccessor>().Current =
            new("native-tests", protocol, protocol.ToString().ToLowerInvariant(), null, "/" + protocol, new Uri("https://registry.test/"));
        var filter = new NativeAuthorizationFilter(protocol, FeedOperation.Pull);
        await filter.InvokeAsync(EndpointFilterInvocationContext.Create(http), context =>
        {
            Assert.False(context.HttpContext.User.Identity?.IsAuthenticated);
            Assert.False(context.HttpContext.User.IsInRole("privileged-cookie"));
            return ValueTask.FromResult<object?>(Results.Ok());
        });
    }

    [Theory]
    [InlineData("1.0.0", "01.00.0")]
    [InlineData("1.2.3-1.dev+pre.2", "01.02.03-01.dev+pre.02")]
    public async Task PubAliasesCannotBypassCanonicalVersionPolicy(string version, string alias)
    {
        var policy = new CanonicalPubVersionPolicy();
        await using var host = await NativeTestHost.StartAsync(artifactHandler: policy);
        host.Authenticate("writer");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(NativeRegistryTests.PubTar($"name: example_sdk\nversion: {version}\n")), "file", "archive.tar.gz");
        using var upload = await host.Client.PostAsync("/pub/api/packages/versions/upload", form);
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
        var checksum = upload.Headers.Location!.Query;
        policy.DenyRelease = true;
        host.Authenticate("reader");
        foreach (var value in new[] { version, alias })
        {
            using var response = await host.Client.GetAsync($"/pub/api/packages/example_sdk/versions/{value}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("pub", response.Headers.WwwAuthenticate.ToString());
        }
        host.Authenticate("writer");
        using var finalize = await host.Client.GetAsync($"/pub/api/packages/example_sdk/versions/{alias}/finalize{checksum}");
        Assert.Equal(HttpStatusCode.Forbidden, finalize.StatusCode);
        policy.DenyRelease = false;
        using var allowed = await host.Client.GetAsync($"/pub/api/packages/example_sdk/versions/{alias}/finalize{checksum}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
