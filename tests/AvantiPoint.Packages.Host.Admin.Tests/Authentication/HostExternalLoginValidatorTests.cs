using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authentication;

public class HostExternalLoginValidatorTests
{
    [Fact]
    public async Task MicrosoftAccount_RejectsPublicTenantConfiguration()
    {
        var validator = CreateValidator(new HostAuthenticationOptions
        {
            Microsoft = new HostMicrosoftOptions { TenantId = "common" },
        });

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "user@company.com"),
            new Claim("tid", "common"),
        ]));

        var result = await validator.ValidateAsync(principal, HostExternalAuthProvider.MicrosoftAccount);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MicrosoftAccount_RequiredGroupMustMatch()
    {
        var validator = CreateValidator(new HostAuthenticationOptions
        {
            Microsoft = new HostMicrosoftOptions
            {
                TenantId = "11111111-1111-1111-1111-111111111111",
                RequiredGroupIds = ["group-a"],
            },
        });

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tid", "11111111-1111-1111-1111-111111111111"),
            new Claim("groups", "group-b"),
        ]));

        var result = await validator.ValidateAsync(principal, HostExternalAuthProvider.MicrosoftAccount);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Google_RequiresHostedDomainClaim()
    {
        var validator = CreateValidator(new HostAuthenticationOptions
        {
            Google = new HostGoogleOptions { HostedDomain = "company.com" },
        });

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("hd", "other.com")]));
        var result = await validator.ValidateAsync(principal, HostExternalAuthProvider.Google);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Google_AllowsWorkspaceDomain_WhenRequiredGroupIdsEmpty()
    {
        var validator = CreateValidator(new HostAuthenticationOptions
        {
            Google = new HostGoogleOptions { HostedDomain = "company.com" },
        });

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("hd", "company.com")]));
        var result = await validator.ValidateAsync(principal, HostExternalAuthProvider.Google);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Google_RejectsWhenRequiredGroupIdsConfigured()
    {
        var validator = CreateValidator(new HostAuthenticationOptions
        {
            Google = new HostGoogleOptions
            {
                HostedDomain = "company.com",
                RequiredGroupIds = ["admins@company.com"],
            },
        });

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("hd", "company.com")]));
        var result = await validator.ValidateAsync(principal, HostExternalAuthProvider.Google);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MicrosoftAccount_AllowsConsumerEmail_WhenTenantConfiguredAndNoRequiredGroups()
    {
        var tenantId = "11111111-1111-1111-1111-111111111111";
        var validator = CreateValidator(new HostAuthenticationOptions
        {
            Microsoft = new HostMicrosoftOptions { TenantId = tenantId },
        });

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "guest@live.com"),
            new Claim("tid", tenantId),
        ]));

        var result = await validator.ValidateAsync(principal, HostExternalAuthProvider.MicrosoftAccount);
        Assert.True(result.Succeeded);
    }

    private static HostExternalLoginValidator CreateValidator(HostAuthenticationOptions options)
    {
        return new HostExternalLoginValidator(
            Options.Create(options),
            new StubHttpClientFactory(),
            NullLogger<HostExternalLoginValidator>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new HttpClientHandler());
    }
}
