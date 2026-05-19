namespace AvantiPoint.Feed.Platform;

public sealed class FeedRegistry : IFeedRegistry
{
    private static readonly HashSet<string> ReservedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "v3", "api", "shield", "npm",
    };

    private readonly List<SurfaceRegistration> _surfaces = [];
    private SurfaceRegistration? _defaultOci;

    public FeedRegistry(FeedContext feed)
    {
        Feed = feed ?? throw new ArgumentNullException(nameof(feed));
    }

    public FeedContext Feed { get; }

    public IReadOnlyList<SurfaceRegistration> Surfaces => _surfaces;

    public SurfaceRegistration? TryGetNuGetSurface() =>
        _surfaces.FirstOrDefault(s => s.Protocol == FeedProtocol.NuGet);

    public SurfaceRegistration? TryGetDefaultOciSurface() => _defaultOci;

    public SurfaceRegistration? TryGetOciSurfaceBySegment(string segment) =>
        _surfaces.FirstOrDefault(s =>
            s.Protocol == FeedProtocol.Oci
            && string.Equals(s.OciSegment, segment, StringComparison.OrdinalIgnoreCase));

    public SurfaceRegistration? TryGetNpmSurface() =>
        _surfaces.FirstOrDefault(s => s.Protocol == FeedProtocol.Npm);

    public bool IsRegistered(string surfaceId) =>
        _surfaces.Any(s => string.Equals(s.SurfaceId, surfaceId, StringComparison.OrdinalIgnoreCase));

    public void Register(SurfaceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (_surfaces.Any(s => string.Equals(s.SurfaceId, registration.SurfaceId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Surface '{registration.SurfaceId}' is already registered.");
        }

        if (!string.IsNullOrEmpty(registration.OciSegment))
        {
            if (ReservedSegments.Contains(registration.OciSegment))
            {
                throw new InvalidOperationException(
                    $"OCI segment '{registration.OciSegment}' is reserved.");
            }

            if (_surfaces.Any(s =>
                    s.Protocol == FeedProtocol.Oci
                    && string.Equals(s.OciSegment, registration.OciSegment, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"OCI segment '{registration.OciSegment}' is already registered.");
            }
        }

        if (registration.Protocol == FeedProtocol.Oci && string.IsNullOrEmpty(registration.OciSegment))
        {
            if (_defaultOci is not null)
            {
                throw new InvalidOperationException("A default OCI surface is already registered.");
            }

            _defaultOci = registration;
        }

        _surfaces.Add(registration);
    }
}
