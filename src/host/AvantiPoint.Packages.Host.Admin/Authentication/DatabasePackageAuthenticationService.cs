using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Configuration;
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
    IOptions<FeedOptions> feedOptions,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DatabasePackageAuthenticationService> logger,
    ISurfaceContextAccessor surfaceAccessor,
    IFeedRegistry registry)
    : IPackageAuthenticationService, IOperationAwarePackageAuthenticationService
{
    public Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken) =>
        AuthenticateTokenAsync(apiKey, FeedOperation.Push, username: null, includeRealm: false, cancellationToken);

    public Task<NuGetAuthenticationResult> AuthenticateAsync(
        string username,
        string token,
        CancellationToken cancellationToken) =>
        AuthenticateTokenAsync(token, FeedOperation.Pull, username, includeRealm: true, cancellationToken);

    public Task<NuGetAuthenticationResult> AuthenticateAsync(
        string token,
        FeedOperation operation,
        CancellationToken cancellationToken = default) =>
        AuthenticateTokenAsync(token, operation, username: null, includeRealm: false, cancellationToken);

    public Task<NuGetAuthenticationResult> AuthenticateAsync(
        string username,
        string token,
        FeedOperation operation,
        CancellationToken cancellationToken = default) =>
        AuthenticateTokenAsync(token, operation, username, includeRealm: true, cancellationToken);

    private async Task<NuGetAuthenticationResult> AuthenticateTokenAsync(
        string plaintext,
        FeedOperation operation,
        string? username,
        bool includeRealm,
        CancellationToken cancellationToken)
    {
        if (MatchesBootstrapApiKey(plaintext))
        {
            return BootstrapSuccess();
        }

        var authToken = await FindTokenAsync(plaintext, cancellationToken);
        if (authToken is null
            || !authToken.IsValid()
            || (username is not null && !string.Equals(authToken.User.Email, username, StringComparison.OrdinalIgnoreCase)))
        {
            return Fail("Invalid or expired credentials", includeRealm);
        }

        if (authToken.User.ApprovalStatus != HostUserApprovalStatus.Approved || authToken.User.IsRevoked)
        {
            return Fail("User is not authorized", includeRealm);
        }

        if (!CanAccessSurface(authToken, operation))
        {
            return Fail("Token is not authorized for this feed", includeRealm);
        }

        if (operation == FeedOperation.Login)
        {
            var mayRead = authToken.Scopes.HasFlag(FeedTokenScope.Read) && authToken.User.CanConsume;
            var mayWrite = authToken.Scopes.HasFlag(FeedTokenScope.Write) && authToken.User.CanPublish;
            return mayRead || mayWrite ? Success(authToken) : Fail("Token has no active permissions", includeRealm);
        }

        if (operation == FeedOperation.Push)
        {
            if (!authToken.Scopes.HasFlag(FeedTokenScope.Write) || !authToken.User.CanPublish)
            {
                return Fail("Token does not have publish permission", includeRealm);
            }
        }
        else if (!authToken.Scopes.HasFlag(FeedTokenScope.Read) || !authToken.User.CanConsume)
        {
            return Fail("Token does not have read permission", includeRealm);
        }

        return Success(authToken);
    }

    private async Task<HostApiToken?> FindTokenAsync(string plaintext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        var prefix = plaintext.Length >= 8 ? plaintext[..8] : plaintext;
        var candidates = await db.HostApiTokens
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Feeds)
            .Where(x => x.TokenPrefix == prefix && !x.Revoked)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(x => tokenHasher.Verify(plaintext, x.TokenHash));
    }

    private bool CanAccessSurface(HostApiToken token, FeedOperation operation)
    {
        if (token.AllFeeds) return true;
        var surface = surfaceAccessor.Current;
        if (surface is null) return false;

        return token.Feeds.Any(grant =>
            string.Equals(grant.FeedId, surface.FeedId, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(grant.SurfaceId, surface.SurfaceId, StringComparison.OrdinalIgnoreCase)
                // Docker and Helm log in through the shared registry version probe.
                // Login grants no artifact access; every artifact route rechecks its surface.
                || operation == FeedOperation.Login && surface.Protocol == FeedProtocol.Oci
                    && registry.Surfaces.Any(s => s.Protocol == FeedProtocol.Oci
                        && string.Equals(s.SurfaceId, grant.SurfaceId, StringComparison.OrdinalIgnoreCase))));
    }

    private NuGetAuthenticationResult Success(HostApiToken token)
    {
        var identity = new ClaimsIdentity("HostFeedAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, token.User.Name));
        identity.AddClaim(new Claim(ClaimTypes.Email, token.User.Email));
        identity.AddClaim(new Claim(FeedClaims.Token, token.TokenPrefix));

        if (!token.IsSystemToken)
        {
            identity.AddClaim(new Claim(FeedClaims.TokenDescription, token.Description));
        }

        if (token.Scopes.HasFlag(FeedTokenScope.Read) && token.User.CanConsume)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, FeedRoles.Consumer));
        }

        if (token.Scopes.HasFlag(FeedTokenScope.Write) && token.User.CanPublish)
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

    private NuGetAuthenticationResult BootstrapSuccess()
    {
        var identity = new ClaimsIdentity("HostBootstrapFeedAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, "Configured feed API key"));
        identity.AddClaim(new Claim(ClaimTypes.Role, FeedRoles.Consumer));
        identity.AddClaim(new Claim(ClaimTypes.Role, FeedRoles.Publisher));
        return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
    }

    private bool MatchesBootstrapApiKey(string candidate)
    {
        var configured = feedOptions.Value.Authentication.ApiKey;
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        // Compare fixed-size hashes to avoid leaking a configured bootstrap credential through
        // early-exit string comparison timing.
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        return CryptographicOperations.FixedTimeEquals(configuredHash, candidateHash);
    }

    private NuGetAuthenticationResult Fail(string message, bool includeRealm)
    {
        var realm = includeRealm ? $"{settings.Value.ServerName} Package Registry" : null;
        logger.LogWarning("Authentication failed: {Message}", message);
        return NuGetAuthenticationResult.Fail(message, settings.Value.ServerName, realm);
    }
}
