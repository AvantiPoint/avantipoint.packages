namespace AvantiPoint.Feed.Platform;

public sealed class FeedScope : Packages.Core.IFeedScope
{
    private readonly IFeedRegistry _registry;

    public FeedScope(IFeedRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public string FeedId => _registry.Feed.FeedId;
}
