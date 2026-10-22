using AvantiPoint.Packages.Core.Entities.Oci;

namespace AvantiPoint.Packages.UI.Services;

public interface IOciRepositoryBrowseService
{
    Task<OciRepositorySearchResult> SearchRepositoriesAsync(
        string? segment,
        string? query = null,
        string? kind = null,
        int skip = 0,
        int take = 24,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OciRepositoryListItem>> ListRepositoriesAsync(
        string? segment,
        int? max = null,
        string? last = null,
        CancellationToken cancellationToken = default);

    Task<OciRepositoryTagsModel?> ListTagsAsync(
        string repositoryName,
        string? segment,
        int? max = null,
        string? last = null,
        CancellationToken cancellationToken = default);

    Task<OciArtifactDetailModel?> GetArtifactAsync(
        string repositoryName,
        string reference,
        string? segment,
        CancellationToken cancellationToken = default);

    string GetRegistryRootUrl(string? segment);

    string GetRegistryApiUrl(string? segment);
}
