// Test-only projection of the members used by token route registration.
namespace AvantiPoint.Feed.Platform;
public interface IFeedRegistry { IReadOnlyList<SurfaceContext> Surfaces { get; } }
