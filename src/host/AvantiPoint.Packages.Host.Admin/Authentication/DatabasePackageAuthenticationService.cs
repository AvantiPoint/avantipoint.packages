using System.Security.Claims;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Authentication;

public sealed class DatabasePackageAuthenticationService(
    IHostIdentityContext db,
    IHostTokenHasher tokenHasher,
    IOptions<HostSettings> settings,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DatabasePackageAuthenticationService> logger) : IPackageAuthenticationService
{
    public async Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        var token = await FindTokenAsync(apiKey, cancellationToken);
        if (token is null || !token.IsValid())
        {
            return Fail("Invalid or expired API key", includeRealm: false);
        }

        if (token.User.ApprovalStatus != HostUserApprovalStatus.Approved || token.User.IsRevoked)
        {
            return Fail("User is not authorized", includeRealm: false);
        }

        if (!token.Scopes.HasFlag(FeedTokenScope.Write) || !token.User.CanPublish)
        {
            return Fail("Token does not have publish permission", includeRealm: false);
        }

        return Success(token, includeRealm: false);
    }

    public async Task<NuGetAuthenticationResult> AuthenticateAsync(
        string username,
        string token,
        CancellationToken cancellationToken)
    {
        var authToken = await FindTokenAsync(token, cancellationToken);
        if (authToken is null || !authToken.IsValid() || authToken.User.Email != username)
        {
            return Fail("Invalid or expired credentials", includeRealm: true);
        }

        if (authToken.User.ApprovalStatus != HostUserApprovalStatus.Approved || authToken.User.IsRevoked)
        {
            return Fail("User is not authorized", includeRealm: true);
        }

        if (!authToken.Scopes.HasFlag(FeedTokenScope.Read) || !authToken.User.CanConsume)
        {
            return Fail("Token does not have read permission", includeRealm: true);
        }

        return Success(authToken, includeRealm: true);
    }

    private async Task<HostApiToken?> FindTokenAsync(string plaintext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        var prefix = plaintext.Length >= 8 ? plaintext[..8] : plaintext;
        var candidates = await db.HostApiTokens
            .Include(x => x.User)
            .Where(x => x.TokenPrefix == prefix && !x.Revoked)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(x => tokenHasher.Verify(plaintext, x.TokenHash));
    }

    private NuGetAuthenticationResult Success(HostApiToken token, bool includeRealm)
    {
        var identity = new ClaimsIdentity("HostFeedAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, token.User.Name));
        identity.AddClaim(new Claim(ClaimTypes.Email, token.User.Email));
        identity.AddClaim(new Claim(FeedClaims.Token, token.TokenPrefix));

        if (!token.IsSystemToken)
        {
            identity.AddClaim(new Claim(FeedClaims.TokenDescription, token.Description));
        }

        identity.AddClaim(new Claim(ClaimTypes.Role, FeedRoles.Consumer));
        if (token.User.CanPublish)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, FeedRoles.Publisher));
        }

        if (token.User.IsAdmin)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, FeedRoles.Admin));
        }

        var remoteIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        logger.LogInformation("Authenticated {User} from {Ip}", token.User.Email, remoteIp);
        return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
    }

    private NuGetAuthenticationResult Fail(string message, bool includeRealm)
    {
        var realm = includeRealm ? $"{settings.Value.ServerName} Package Registry" : null;
        logger.LogWarning("Authentication failed: {Message}", message);
        return NuGetAuthenticationResult.Fail(message, settings.Value.ServerName, realm);
    }
}
