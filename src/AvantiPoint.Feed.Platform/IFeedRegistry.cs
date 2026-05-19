namespace AvantiPoint.Feed.Platform;

public interface IFeedRegistry
{
    FeedContext Feed { get; }

    IReadOnlyList<SurfaceRegistration> Surfaces { get; }

    SurfaceRegistration? TryGetNuGetSurface();

    SurfaceRegistration? TryGetDefaultOciSurface();

    SurfaceRegistration? TryGetOciSurfaceBySegment(string segment);

    SurfaceRegistration? TryGetNpmSurface();

    void Register(SurfaceRegistration registration);

    bool IsRegistered(string surfaceId);
}
