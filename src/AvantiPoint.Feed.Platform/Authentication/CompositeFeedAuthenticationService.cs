namespace AvantiPoint.Feed.Platform.Authentication;

public sealed class CompositeFeedAuthenticationService : IFeedAuthenticationService
{
    private readonly IReadOnlyDictionary<FeedProtocol, IFeedProtocolAuthenticationAdapter> _adapters;

    public CompositeFeedAuthenticationService(IEnumerable<IFeedProtocolAuthenticationAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(a => a.Protocol);
    }

    public Task<FeedAuthenticationResult> AuthenticateAsync(
        FeedAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_adapters.TryGetValue(request.Surface.Protocol, out var adapter))
        {
            return Task.FromResult(FeedAuthenticationResult.Fail(
                $"No authentication adapter registered for protocol '{request.Surface.Protocol}'."));
        }

        return adapter.AuthenticateAsync(request, cancellationToken);
    }
}

public interface IFeedProtocolAuthenticationAdapter : IFeedAuthenticationService
{
    FeedProtocol Protocol { get; }
}
