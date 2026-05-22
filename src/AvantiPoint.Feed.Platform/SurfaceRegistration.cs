namespace AvantiPoint.Feed.Platform;

/// <summary>
/// A feed surface registered at application startup.
/// </summary>
public sealed record SurfaceRegistration(
    string SurfaceId,
    FeedProtocol Protocol,
    string? OciSegment,
    string RoutePrefix,
    string OptionsSectionKey,
    /// <summary>
    /// When true, also match Helm-style <c>/v2/{segment}/...</c> paths (opt-in; can shadow default-surface repos named like the segment).
    /// </summary>
    bool AllowV2EmbeddedSegmentRouting = false);
