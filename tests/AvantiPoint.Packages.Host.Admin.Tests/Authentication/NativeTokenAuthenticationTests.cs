using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authentication;

public sealed class NativeTokenAuthenticationTests
{
    [Theory]
    [InlineData(FeedTokenScope.Read, FeedOperation.Pull, true, 401)]
    [InlineData(FeedTokenScope.Read, FeedOperation.Push, false, 403)]
    [InlineData(FeedTokenScope.Write, FeedOperation.Pull, false, 403)]
    [InlineData(FeedTokenScope.Write, FeedOperation.Push, true, 401)]
    [InlineData(FeedTokenScope.ReadWrite, FeedOperation.Login, false, 403)]
    public async Task EnforcesTokenScopesWithoutRequiringBearerUsername(
        FeedTokenScope scope, FeedOperation operation, bool expected, int failureStatus)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var user = new HostUser
        {
            Email = "native@example.test", Name = "Native test", CanConsume = true, CanPublish = true,
            ApprovalStatus = HostUserApprovalStatus.Approved,
        };
        var hasher = new HostTokenHasher(Options.Create(new HostSettings()));
        var generated = hasher.GenerateToken();
        var token = generated.Plaintext;
        db.HostApiTokens.Add(new HostApiToken
        {
            User = user, UserEmail = user.Email, TokenPrefix = token[..8], TokenHash = generated.Hash,
            Scopes = scope, Created = DateTimeOffset.UtcNow, Expires = DateTimeOffset.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new DatabasePackageAuthenticationService(db, hasher, Options.Create(new HostSettings()),
            new HttpContextAccessor(), NullLogger<DatabasePackageAuthenticationService>.Instance);
        var result = await service.AuthenticateTokenAsync(token, operation, null, TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Succeeded);
        if (!expected) Assert.Equal(failureStatus, result.FailureStatusCode);
        var wrongUsername = await service.AuthenticateTokenAsync(token, operation, "other@example.test", TestContext.Current.CancellationToken);
        Assert.False(wrongUsername.Succeeded);
        Assert.Equal(401, wrongUsername.FailureStatusCode);
        user.IsRevoked = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var revoked = await service.AuthenticateTokenAsync(token, operation, null, TestContext.Current.CancellationToken);
        Assert.False(revoked.Succeeded);
        Assert.Equal(403, revoked.FailureStatusCode);
    }
}
