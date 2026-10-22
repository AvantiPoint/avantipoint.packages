using System.Security.Claims;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Core;

namespace ComponentRegression.TestInfrastructure;

internal sealed class SyntheticPackageAuthentication : IPackageAuthenticationService, IOperationAwarePackageAuthenticationService, IFeedAuthenticationService
{
    internal SurfaceContext Surface { get; set; } = ScopedTokenTestHost.DefaultSurface;
    internal bool Revoked { get; set; }
    internal bool AllFeeds { get; set; }
    internal bool AnonymousPull { get; set; }

    public Task<NuGetAuthenticationResult> AuthenticateAsync(string token, CancellationToken ct) =>
        AuthenticateAsync(token, FeedOperation.Push, ct);
    public Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken ct) =>
        AuthenticateAsync(username, token, FeedOperation.Pull, ct);
    public Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, FeedOperation operation, CancellationToken ct = default) =>
        username == "user@example.invalid" ? AuthenticateAsync(token, operation, ct) : Task.FromResult(Failure());
    public Task<NuGetAuthenticationResult> AuthenticateAsync(string token, FeedOperation operation, CancellationToken ct = default)
    {
        var surfaceAllowed = AllFeeds || Surface.FeedId == "default" && Surface.SurfaceId == "oci-docker";
        var permitted = !Revoked && surfaceAllowed && (operation switch
        {
            FeedOperation.Login => token is "consumer" or "publisher" or "both" || token.Length == 0 && AnonymousPull,
            FeedOperation.Pull => token is "consumer" or "both" || token.Length == 0 && AnonymousPull,
            FeedOperation.Push => token is "publisher" or "both",
            _ => false,
        });
        return Task.FromResult(permitted ? NuGetAuthenticationResult.Success(new ClaimsPrincipal(new ClaimsIdentity("fixture"))) : Failure());
    }
    public async Task<FeedAuthenticationResult> AuthenticateAsync(FeedAuthenticationRequest request, CancellationToken ct = default)
    {
        Surface = request.Surface;
        var token = request.HttpContext.Request.Headers.Authorization.ToString();
        token = token.StartsWith("Bearer ", StringComparison.Ordinal) ? token[7..] : string.Empty;
        var result = await AuthenticateAsync(token, request.Operation, ct);
        return result.Succeeded ? FeedAuthenticationResult.Success(result.User) : FeedAuthenticationResult.Fail("Denied.");
    }
    private static NuGetAuthenticationResult Failure() => NuGetAuthenticationResult.Fail("Denied.", "fixture");
}
