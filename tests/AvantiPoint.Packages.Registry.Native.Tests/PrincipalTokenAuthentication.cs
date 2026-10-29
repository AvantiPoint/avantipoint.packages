using System.Security.Claims;
using AvantiPoint.Feed.Platform.Authentication;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class PrincipalTokenAuthentication : IFeedTokenAuthenticationService
{
    public ClaimsPrincipal User { get; } = new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "feed-reader")], "FeedToken"));

    public Task<FeedAuthenticationResult> AuthenticateTokenAsync(string token, FeedOperation operation,
        string? username, CancellationToken cancellationToken = default) =>
        Task.FromResult(FeedAuthenticationResult.Success(User));
}
