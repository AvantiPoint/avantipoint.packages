namespace AvantiPoint.Feed.Platform.Authentication;

public interface IFeedAuthenticationService
{
    Task<FeedAuthenticationResult> AuthenticateAsync(
        FeedAuthenticationRequest request,
        CancellationToken cancellationToken = default);
}
