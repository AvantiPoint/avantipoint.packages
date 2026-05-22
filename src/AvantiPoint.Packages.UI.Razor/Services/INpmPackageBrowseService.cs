namespace AvantiPoint.Packages.UI.Services;

public interface INpmPackageBrowseService
{
    Task<IReadOnlyList<NpmPackageListItem>> SearchAsync(
        string? query,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<NpmPackageDetailModel?> GetPackageAsync(
        string packageName,
        CancellationToken cancellationToken = default);
}
