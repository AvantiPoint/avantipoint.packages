namespace AvantiPoint.Packages.UI.Services;

public interface IArtifactDiscoveryService
{
    Task<ArtifactDiscoveryResult> SearchAsync(
        string? query,
        int takePerSurface = 6,
        CancellationToken cancellationToken = default);
}
