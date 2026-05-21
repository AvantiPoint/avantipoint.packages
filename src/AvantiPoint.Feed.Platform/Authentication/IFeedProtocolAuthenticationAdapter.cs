namespace AvantiPoint.Feed.Platform.Authentication;

public interface IFeedProtocolAuthenticationAdapter : IFeedAuthenticationService
{
    FeedProtocol Protocol { get; }
}
