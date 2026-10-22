using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Admin.Services.Tokens;

public sealed class HostApiTokenService(IHostIdentityContext db, IHostTokenHasher hasher, IFeedRegistry registry)
{
    public async Task<string> CreateAsync(string email, string? description, FeedTokenScope scopes,
        bool allFeeds, IEnumerable<string> surfaceIds, int expirationDays, CancellationToken cancellationToken = default)
    {
        var user = await db.HostUsers.SingleAsync(u => u.Email == email, cancellationToken);
        if (user.IsRevoked || user.ApprovalStatus != HostUserApprovalStatus.Approved)
            throw new UnauthorizedAccessException("Your account is not approved for access.");
        if (scopes == FeedTokenScope.None || (scopes & ~FeedTokenScope.ReadWrite) != 0)
            throw new ArgumentException("Select at least one permission: read or write.");
        if (scopes.HasFlag(FeedTokenScope.Read) && !user.CanConsume)
            throw new UnauthorizedAccessException("Your account does not have read access.");
        if (scopes.HasFlag(FeedTokenScope.Write) && !user.CanPublish)
            throw new UnauthorizedAccessException("Your account does not have publishing access.");
        if (expirationDays is < 1 or > 365)
            throw new ArgumentException("Token expiration must be between 1 and 365 days.");
        if (description?.Length > 128)
            throw new ArgumentException("Token names must be 128 characters or fewer.");

        var selections = allFeeds ? [] : surfaceIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!allFeeds && (selections.Length == 0 || selections.Any(id => !registry.IsRegistered(id))))
            throw new ArgumentException("Select at least one configured feed, or choose all feeds.");

        var (plaintext, prefix, hash) = hasher.GenerateToken();
        db.HostApiTokens.Add(new HostApiToken
        {
            Description = string.IsNullOrWhiteSpace(description) ? "API Token" : description.Trim(),
            UserEmail = user.Email,
            TokenPrefix = prefix,
            TokenHash = hash,
            Created = DateTimeOffset.UtcNow,
            Expires = DateTimeOffset.UtcNow.AddDays(expirationDays),
            Scopes = scopes,
            AllFeeds = allFeeds,
            Feeds = selections.Select(id => new HostApiTokenFeed
            {
                FeedId = registry.Feed.FeedId,
                SurfaceId = registry.Surfaces.Single(s => string.Equals(s.SurfaceId, id, StringComparison.OrdinalIgnoreCase)).SurfaceId,
            }).ToList(),
        });
        db.HostAuditEvents.Add(new HostAuditEvent
        {
            Actor = email, EventType = "token.created", Subject = prefix,
            Timestamp = DateTimeOffset.UtcNow,
            Detail = $"permissions={scopes}; feeds={(allFeeds ? "all" : string.Join(",", selections))}; expiresInDays={expirationDays}",
        });
        await db.SaveChangesAsync(cancellationToken);
        return plaintext;
    }

    public async Task<bool> RevokeAsync(string email, int id, CancellationToken cancellationToken = default)
    {
        var token = await db.HostApiTokens.SingleOrDefaultAsync(t => t.Id == id && t.UserEmail == email && !t.IsSystemToken, cancellationToken);
        if (token is null) return false;
        if (token.Revoked) return true;
        token.Revoked = true;
        db.HostAuditEvents.Add(new HostAuditEvent
        {
            Actor = email, EventType = "token.revoked", Subject = token.TokenPrefix, Timestamp = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
