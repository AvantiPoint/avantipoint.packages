using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authentication;

public sealed class HostUiAuthorizationPolicyTests
{
    [Fact]
    public async Task AnonymousUser_CannotBrowseEvenWhenProtocolAnonymousPullIsEnabled()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(await IsAuthorizedAsync(allowAnonymousPull: true, anonymous));
        Assert.False(await IsAuthorizedAsync(allowAnonymousPull: false, anonymous));
    }

    [Theory]
    [InlineData(FeedRoles.Consumer, true)]
    [InlineData(FeedRoles.Admin, true)]
    [InlineData(FeedRoles.Publisher, false)]
    [InlineData(null, false)]
    public async Task AuthenticatedUser_RequiresConsumerOrAdminRole(string? role, bool expected)
    {
        var claims = role is null
            ? Array.Empty<Claim>()
            : [new Claim(ClaimTypes.Role, role)];
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        Assert.Equal(expected, await IsAuthorizedAsync(allowAnonymousPull: false, user));
    }

    [Theory]
    [InlineData(FeedRoles.Consumer)]
    [InlineData(FeedRoles.Admin)]
    [InlineData(FeedRoles.Publisher)]
    [InlineData(null)]
    public async Task PublicProtocolFeed_PreservesAccessForSignedInUsers(string? role)
    {
        var claims = role is null ? Array.Empty<Claim>() : [new Claim(ClaimTypes.Role, role)];
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        Assert.True(await IsAuthorizedAsync(allowAnonymousPull: true, user));
    }

    private static async Task<bool> IsAuthorizedAsync(
        bool allowAnonymousPull,
        ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
            options.AddPolicy(
                HostUiAuthorizationPolicy.Name,
                HostUiAuthorizationPolicy.Create(allowAnonymousPull)));

        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(
            user,
            resource: null,
            HostUiAuthorizationPolicy.Name);
        return result.Succeeded;
    }
}
