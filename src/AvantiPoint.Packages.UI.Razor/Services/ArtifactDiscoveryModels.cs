namespace AvantiPoint.Packages.UI.Services;

public enum ArtifactDiscoveryKind
{
    NuGet,
    Npm,
    Docker,
    Helm,
    Oci,
}

public sealed record ArtifactDiscoveryItem(
    string Name,
    string? Version,
    string? Description,
    string Href,
    DateTimeOffset? PublishedAt = null,
    string? Detail = null);

public sealed record ArtifactDiscoverySection(
    string Id,
    string Name,
    ArtifactDiscoveryKind Kind,
    string Description,
    string BrowseHref,
    string ConnectHref,
    long? TotalCount,
    bool HasMore,
    IReadOnlyList<ArtifactDiscoveryItem> Items,
    string? ErrorMessage = null)
{
    public bool IsAvailable => string.IsNullOrEmpty(ErrorMessage);
}

public sealed record ArtifactDiscoveryResult(
    string Query,
    IReadOnlyList<ArtifactDiscoverySection> Sections);
