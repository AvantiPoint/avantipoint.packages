namespace AvantiPoint.Feed.Platform.Callbacks;

public sealed class CompositeFeedActionHandler : IFeedActionHandler
{
    private readonly IEnumerable<IFeedActionHandler> _handlers;

    public CompositeFeedActionHandler(IEnumerable<IFeedActionHandler> handlers)
    {
        _handlers = handlers;
    }

    public async Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        var handlers = _handlers as ICollection<IFeedActionHandler> ?? _handlers.ToList();
        if (handlers.Count == 0)
        {
            return true;
        }

        foreach (var handler in handlers)
        {
            if (await handler.CanAccessArtifact(context, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    public async Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        foreach (var handler in _handlers)
        {
            await handler.OnArtifactDownloaded(context, cancellationToken);
        }
    }

    public async Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
    {
        foreach (var handler in _handlers)
        {
            await handler.OnArtifactUploaded(context, cancellationToken);
        }
    }
}
