using System.Security.Claims;

namespace AvantiPoint.Feed.Platform.Authentication;

public sealed record FeedAuthenticationResult(
    bool Succeeded,
    ClaimsPrincipal? User = null,
    string? Message = null,
    IReadOnlyDictionary<string, string>? ResponseHeaders = null)
{
    public static FeedAuthenticationResult Success(ClaimsPrincipal? user = null) =>
        new(true, user);

    public static FeedAuthenticationResult Fail(
        string message,
        IReadOnlyDictionary<string, string>? responseHeaders = null) =>
        new(false, Message: message, ResponseHeaders: responseHeaders);
}
