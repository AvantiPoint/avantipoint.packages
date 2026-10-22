using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authorization;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authentication;

public sealed class EntraDeploymentAccessTests
{
    private const string TenantId = "11111111-1111-4111-8111-111111111111";
    private const string DevelopersId = "22222222-2222-4222-8222-222222222222";
    private const string AdminsId = "33333333-3333-4333-8333-333333333333";
    private const string PublishersId = "44444444-4444-4444-8444-444444444444";

    [Fact]
    public void Profile_RequiresDevelopersAndKeepsRolesExternallyManaged()
    {
        using var configuration = LoadProfile();
        var options = Authentication(configuration);

        Assert.Equal(TenantId, options.Microsoft.TenantId);
        Assert.Equal([DevelopersId], options.Microsoft.RequiredGroupIds);
        Assert.Equal([DevelopersId], options.Microsoft.ConsumerRoleGroupIds);
        Assert.Equal([AdminsId], options.Microsoft.AdminRoleGroupIds);
        Assert.Equal([PublishersId], options.Microsoft.PublisherRoleGroupIds);
        Assert.False(configuration.GetValue<bool>("Feed:Authentication:AllowAnonymousPull", true));
        Assert.False(configuration.GetValue<bool>("Host:Access:RequireNewUserApproval", true));
        Assert.True(HostRoleAssignmentPolicy.AdminIsManagedByEntra(options));
        Assert.True(HostRoleAssignmentPolicy.PublishingIsManagedByEntra(options));
        Assert.True(string.IsNullOrEmpty(options.Microsoft.ClientId));
        Assert.True(string.IsNullOrEmpty(options.Microsoft.ClientSecret));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task Profile_EnforcesLoginGateAndIndependentRoles(
        bool developer, bool admin, bool publisher)
    {
        using var configuration = LoadProfile();
        var options = Authentication(configuration);
        var memberships = new List<string>();
        if (developer) memberships.Add(DevelopersId);
        if (admin) memberships.Add(AdminsId);
        if (publisher) memberships.Add(PublishersId);
        using var factory = new GraphFactory(memberships);
        var validator = Validator(options, factory);
        var principal = Principal();

        var result = await validator.ValidateAsync(principal,
            HostExternalAuthProvider.MicrosoftAccount, "test-only-access-token",
            TestContext.Current.CancellationToken);

        Assert.Equal(developer, result.Succeeded);
        Assert.Equal(1, factory.RequestCount);
        if (!result.Succeeded) return;

        // Previously stored permissions cannot elevate a developer at the next sign-in.
        var user = new HostUser { IsAdmin = true, CanPublish = true };
        HostRoleAssignmentPolicy.ApplyMicrosoftRoles(user, principal, options);
        Assert.Equal(admin, user.IsAdmin);
        Assert.Equal(publisher, user.CanPublish);
        Assert.True(user.CanConsume);
    }

    [Fact]
    public async Task Profile_RejectsUnverifiableMembershipEvenWithExistingGroupClaims()
    {
        using var configuration = LoadProfile();
        using var factory = new GraphFactory([], HttpStatusCode.ServiceUnavailable);
        var principal = Principal();
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("groups", DevelopersId));

        var result = await Validator(Authentication(configuration), factory).ValidateAsync(
            principal, HostExternalAuthProvider.MicrosoftAccount, "test-only-access-token",
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Profile_RejectsAnotherTenantBeforeQueryingGroups()
    {
        using var configuration = LoadProfile();
        using var factory = new GraphFactory([DevelopersId, AdminsId, PublishersId]);

        var result = await Validator(Authentication(configuration), factory).ValidateAsync(
            Principal(Guid.NewGuid().ToString()), HostExternalAuthProvider.MicrosoftAccount,
            "test-only-access-token", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(0, factory.RequestCount);
    }

    private static ConfigurationRoot LoadProfile() => (ConfigurationRoot)new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "entra-access-profile.json"), optional: false)
        .Build();

    private static HostAuthenticationOptions Authentication(IConfiguration configuration) =>
        configuration.GetSection("Host:Authentication").Get<HostAuthenticationOptions>()!;

    private static ClaimsPrincipal Principal(string tenant = TenantId) => new(new ClaimsIdentity(
    [
        new Claim("tid", tenant),
        new Claim(ClaimTypes.Email, "member@example.com"),
    ], "test"));

    private static HostExternalLoginValidator Validator(
        HostAuthenticationOptions options, IHttpClientFactory factory) => new(
            Options.Create(options), factory, NullLogger<HostExternalLoginValidator>.Instance,
            new HostMicrosoftGroupResolver(factory, NullLogger<HostMicrosoftGroupResolver>.Instance));

    private sealed class GraphFactory(List<string> memberships,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler, IHttpClientFactory
    {
        public int RequestCount { get; private set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://graph.microsoft.com/v1.0/me/checkMemberGroups", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(new { value = memberships }),
            });
        }
    }
}
