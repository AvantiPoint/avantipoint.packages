using AvantiPoint.Feed.Platform;
namespace ComponentRegression.TestInfrastructure;
internal sealed class TestSurfaceAccessor : ISurfaceContextAccessor
{
    public SurfaceContext? Current { get; set; }
}
