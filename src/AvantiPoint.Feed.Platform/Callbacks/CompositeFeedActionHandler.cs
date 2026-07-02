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
        // IProtocolNeutralFeedActionHandler implementations (audit logging, webhooks, ...) have no
        // access-control opinion; they always abstain by returning true. Since this method ORs every
        // handler's answer together, letting them vote here would make the whole composite return
        // true unconditionally the moment one is registered - silently bypassing a real handler's
        // (e.g. the NuGet adapter's) deny decision. Only handlers that actually make access decisions
        // get a vote.
        var handlers = _handlers.Where(h => h is not IProtocolNeutralFeedActionHandler).ToList();
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
