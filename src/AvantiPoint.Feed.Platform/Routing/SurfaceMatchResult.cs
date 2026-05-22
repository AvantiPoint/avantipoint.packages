namespace AvantiPoint.Feed.Platform.Routing;

public sealed record SurfaceMatchResult(
    SurfaceRegistration Registration,
    string? PathRemainder,
    bool StripSegmentPrefix,
    bool StripV2EmbeddedSegment = false);
