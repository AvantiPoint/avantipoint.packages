namespace AvantiPoint.Packages.UI.Services;

public sealed record NpmPackageListItem(
    string Name,
    string? LatestVersion,
    DateTime? Published,
    string? Description = null,
    IReadOnlyList<string>? Keywords = null);
