namespace AvantiPoint.Packages.UI.Services;

public sealed record NpmPackageDetailModel(
    string Name,
    IReadOnlyList<NpmVersionListItem> Versions,
    IReadOnlyDictionary<string, string> DistTags);

public sealed record NpmVersionListItem(
    string Version,
    DateTime Published,
    string? Shasum);
