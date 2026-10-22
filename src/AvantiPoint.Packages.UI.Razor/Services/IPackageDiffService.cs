namespace AvantiPoint.Packages.UI.Services;

public interface IPackageDiffService
{
    Task<PackageDiffResult> GetDiffAsync(
        string packageId,
        string currentVersion,
        string? compareVersion,
        bool onlyOneVersionInFeed,
        string? filePath = null,
        string? view = null,
        string? tfm = null,
        CancellationToken cancellationToken = default);
}
