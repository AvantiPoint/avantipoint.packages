namespace AvantiPoint.Packages.UI.Services;

using AvantiPoint.Packages.Core.Entities.Oci;

public sealed record OciRepositoryListItem(string Name);

public sealed record OciRepositoryTagsModel(
    string RepositoryName,
    IReadOnlyList<string> Tags);

public sealed record OciArtifactDetailModel(
    string RepositoryName,
    string Reference,
    string Digest,
    string MediaType,
    OciArtifactKind ArtifactKind,
    string? PlatformOs,
    string? PlatformArch,
    long Size,
    IReadOnlyList<string> Tags,
    string RegistryRootUrl);
