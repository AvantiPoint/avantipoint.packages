namespace AvantiPoint.Feed.Platform;

/// <summary>
/// Resolved surface for the current HTTP request.
/// </summary>
public sealed record SurfaceContext(
    string FeedId,
    FeedProtocol Protocol,
    string SurfaceId,
    string? OciSegment,
    string RoutePrefix,
    Uri PublicBaseUrl);
