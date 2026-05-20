namespace AvantiPoint.Feed.Platform;

/// <summary>
/// A feed surface registered at application startup.
/// </summary>
public sealed record SurfaceRegistration(
    string SurfaceId,
    FeedProtocol Protocol,
    string? OciSegment,
    string RoutePrefix,
    string OptionsSectionKey);
