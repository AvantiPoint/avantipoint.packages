using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using AvantiPoint.Packages.Host.Admin.Authorization;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authentication;

public sealed class HostMicrosoftGroupResolverTests
{
    private const string AdminGroup = "11111111-1111-1111-1111-111111111111";
    private const string PublisherGroup = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task MissingClaims_GraphMembershipGrantsOnlyConfiguredRoles()
    {
        using var factory = new GraphFactory(request =>
            Task.FromResult(Json(new { value = new[] { AdminGroup } })));
        var options = new HostAuthenticationOptions
        {
            Microsoft = new HostMicrosoftOptions
            {
                TenantId = "33333333-3333-3333-3333-333333333333",
                AdminRoleGroupIds = [AdminGroup],
                PublisherRoleGroupIds = [PublisherGroup],
            },
        };
        var resolver = Resolver(factory);
        var validator = new HostExternalLoginValidator(Options.Create(options), factory,
            NullLogger<HostExternalLoginValidator>.Instance, resolver);
        var principal = Principal();

        var validation = await validator.ValidateAsync(principal,
            HostExternalAuthProvider.MicrosoftAccount, "delegated-test-token", TestContext.Current.CancellationToken);
        Assert.True(validation.Succeeded);
        var user = new HostUser();
        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(user, principal, options);
        Assert.True(user.IsAdmin);
        Assert.False(user.CanPublish);
        Assert.True(user.CanConsume);
        Assert.Equal(1, factory.Requests);
    }

    [Fact]
    public async Task OverageClaims_AreResolvedUsingFixedGraphEndpointInBatches()
    {
        var groups = Enumerable.Range(0, 43).Select(_ => Guid.NewGuid().ToString()).ToList();
        using var factory = new GraphFactory(async request =>
        {
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>();
            var batch = body.GetProperty("groupIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            Assert.InRange(batch.Length, 1, 20);
            return Json(new { value = batch });
        });
        var principal = Principal(new Claim("_claim_names", "{\"groups\":\"src1\"}"),
            new Claim("_claim_sources", "{\"src1\":{\"endpoint\":\"https://untrusted.invalid\"}}"));

        var result = await Resolver(factory).ResolveAsync(principal,
            new HostMicrosoftOptions { AdminRoleGroupIds = groups, RequiredGroupIds = [groups[0]] },
            "delegated-test-token", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(3, factory.Requests);
        Assert.Equal(groups.Order(), principal.FindAll("groups").Select(c => c.Value).Order());
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task GraphFailure_DoesNotApplyPartialOrEmptyMembership(HttpStatusCode status)
    {
        using var factory = new GraphFactory(_ => Task.FromResult(new HttpResponseMessage(status)));
        var principal = Principal(new Claim("groups", AdminGroup));
        var result = await Resolver(factory).ResolveAsync(principal,
            new HostMicrosoftOptions { AdminRoleGroupIds = [AdminGroup] },
            "delegated-test-token", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AdminGroup, Assert.Single(principal.FindAll("groups")).Value);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"value\":null}")]
    [InlineData("{\"value\":[\"unrequested-group\"]}")]
    [InlineData("not json")]
    public async Task InvalidResponse_FailsClosed(string json)
    {
        using var factory = new GraphFactory(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        }));
        var result = await Resolver(factory).ResolveAsync(Principal(),
            new HostMicrosoftOptions { AdminRoleGroupIds = [AdminGroup] },
            "delegated-test-token", TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task NonMember_RemovesStaleClaims()
    {
        using var factory = new GraphFactory(_ => Task.FromResult(Json(new { value = Array.Empty<string>() })));
        var principal = Principal(new Claim("groups", AdminGroup));
        var result = await Resolver(factory).ResolveAsync(principal,
            new HostMicrosoftOptions { AdminRoleGroupIds = [AdminGroup] },
            "delegated-test-token", TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Assert.Empty(principal.FindAll("groups"));
    }

    [Fact]
    public async Task NoConfiguredGroups_DoesNotRequireGraphOrToken()
    {
        using var factory = new GraphFactory(_ => throw new InvalidOperationException("Unexpected Graph call"));
        Assert.True((await Resolver(factory).ResolveAsync(Principal(), new HostMicrosoftOptions(),
            null, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(0, factory.Requests);
    }

    [Fact]
    public async Task MissingToken_DoesNotTrustExistingGroupClaims()
    {
        using var factory = new GraphFactory(_ => throw new InvalidOperationException("Unexpected Graph call"));
        Assert.False((await Resolver(factory).ResolveAsync(Principal(new Claim("groups", AdminGroup)),
            new HostMicrosoftOptions { AdminRoleGroupIds = [AdminGroup] },
            null, TestContext.Current.CancellationToken)).Succeeded);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static HostMicrosoftGroupResolver Resolver(IHttpClientFactory factory) =>
        new(factory, NullLogger<HostMicrosoftGroupResolver>.Instance);

    private sealed class GraphFactory(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler, IHttpClientFactory
    {
        public int Requests { get; private set; }
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal("https://graph.microsoft.com/v1.0/me/checkMemberGroups", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new AuthenticationHeaderValue("Bearer", "delegated-test-token"), request.Headers.Authorization);
            return respond(request);
        }
    }
}
