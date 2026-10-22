namespace AvantiPoint.Packages.UI.Services;

public interface INpmPackageComparisonService
{
    Task<NpmPackageVersionComparison?> CompareAsync(
        string packageName,
        string baseVersion,
        string targetVersion,
        string? selectedFile = null,
        CancellationToken cancellationToken = default);
}
