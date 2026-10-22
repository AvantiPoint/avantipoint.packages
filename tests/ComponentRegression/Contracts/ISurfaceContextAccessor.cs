namespace AvantiPoint.Feed.Platform;

public interface ISurfaceContextAccessor
{
    SurfaceContext? Current { get; set; }
}
