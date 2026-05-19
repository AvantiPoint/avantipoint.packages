namespace AvantiPoint.Feed.Platform;

public sealed class SurfaceContextAccessor : ISurfaceContextAccessor
{
    public SurfaceContext? Current { get; set; }
}
