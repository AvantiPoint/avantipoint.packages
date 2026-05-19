namespace AvantiPoint.Feed.Platform;

/// <summary>
/// Deployment-wide feed scope (storage prefix, shared authentication).
/// </summary>
public sealed record FeedContext(
    string FeedId,
    string Name,
    string StoragePrefix);
