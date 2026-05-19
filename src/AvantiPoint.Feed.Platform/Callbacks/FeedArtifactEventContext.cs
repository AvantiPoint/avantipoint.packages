namespace AvantiPoint.Feed.Platform.Callbacks;

public sealed record FeedArtifactEventContext(
    SurfaceContext Surface,
    string ArtifactName,
    string? Version,
    string? DigestOrTarballPath);
