namespace AvantiPoint.Packages.UI.Services;

public sealed record NpmPackageVersionComparison(
    string PackageName,
    string BaseVersion,
    string TargetVersion,
    IReadOnlyList<NpmPackageMetadataChange> MetadataChanges,
    string? MetadataUnavailableReason,
    IReadOnlyList<NpmPackageFileChange> FileChanges,
    IReadOnlyList<NpmPackageFileTreeNode> FileTree,
    string ArchiveState,
    string ArchiveMessage,
    string? SelectedFile,
    IReadOnlyList<DiffLine> SelectedFileDiff,
    string? SelectedFilePreviewUnavailableReason)
{
    public int AddedFileCount => FileChanges.Count(static file => file.ChangeKind == "added");

    public int RemovedFileCount => FileChanges.Count(static file => file.ChangeKind == "removed");

    public int ChangedFileCount => FileChanges.Count(static file => file.ChangeKind == "changed");
}

public sealed record NpmPackageMetadataChange(
    string Field,
    string? BaseValue,
    string? TargetValue)
{
    public string ChangeKind => BaseValue is null ? "added" : TargetValue is null ? "removed" : "changed";
}

public sealed record NpmPackageFileChange(
    string Path,
    string ChangeKind,
    long? BaseSize,
    long? TargetSize,
    bool HasTextPreview);

public sealed record NpmPackageFileTreeNode(
    string Name,
    string Path,
    bool IsDirectory,
    string ChangeKind,
    bool HasTextPreview,
    IReadOnlyList<NpmPackageFileTreeNode> Children);
