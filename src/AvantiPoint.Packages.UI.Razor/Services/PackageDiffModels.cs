namespace AvantiPoint.Packages.UI.Services;

public sealed class PackageArchiveFile
{
    public required string Path { get; init; }
    public long Size { get; init; }
    public uint Crc32 { get; init; }
}

public sealed class PackageFileChange
{
    public required string Path { get; init; }
    public required string Kind { get; init; }
    public long? OldSize { get; init; }
    public long? NewSize { get; init; }
    public uint? OldCrc32 { get; init; }
    public uint? NewCrc32 { get; init; }
}

public sealed class PackageNuspecChange
{
    public required string Field { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public sealed class PackageDiffTreeNode
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public string Kind { get; init; } = "file";
    public string Change { get; init; } = "same";
    public IReadOnlyList<PackageDiffTreeNode> Children { get; init; } = [];
}

public sealed class DiffLine
{
    public required string Kind { get; init; }
    public required string Text { get; init; }
    public int? LeftNumber { get; init; }
    public int? RightNumber { get; init; }
}

public sealed class ApiSymbolChange
{
    public required string Name { get; init; }
    public string? Signature { get; init; }
    public string Kind { get; init; } = "type";
}

public sealed class ApiTypeDiff
{
    public required string Name { get; init; }
    public string Kind { get; init; } = "class";
    public IReadOnlyList<ApiSymbolChange> AddedMembers { get; init; } = [];
    public IReadOnlyList<ApiSymbolChange> RemovedMembers { get; init; } = [];
    public IReadOnlyList<ApiSymbolChange> ChangedMembers { get; init; } = [];
}

public sealed class PackageDiffResult
{
    public required string PackageId { get; init; }
    public required string CurrentVersion { get; init; }
    public string? CompareVersion { get; init; }
    public bool HasCompareVersion => !string.IsNullOrWhiteSpace(CompareVersion);
    public string? Message { get; set; }
    public string? Error { get; init; }
    public IReadOnlyList<PackageArchiveFile> CurrentFiles { get; init; } = [];
    public IReadOnlyList<PackageFileChange> FileChanges { get; init; } = [];
    public IReadOnlyList<string> CurrentAssetFolders { get; init; } = [];
    public IReadOnlyList<string> AddedAssetFolders { get; init; } = [];
    public IReadOnlyList<string> RemovedAssetFolders { get; init; } = [];
    public IReadOnlyList<PackageNuspecChange> NuspecChanges { get; init; } = [];
    public IReadOnlyList<PackageDiffTreeNode> Tree { get; init; } = [];
    public string? SelectedFile { get; set; }
    public string? LeftText { get; set; }
    public string? RightText { get; set; }
    public string? TextSource { get; set; }
    public IReadOnlyList<DiffLine> Lines { get; set; } = [];
    public string View { get; init; } = "unified";
    public IReadOnlyList<string> TargetFrameworks { get; set; } = [];
    public string? SelectedTfm { get; set; }
    public string? SelectedType { get; set; }
    public IReadOnlyList<ApiSymbolChange> AddedTypes { get; set; } = [];
    public IReadOnlyList<ApiSymbolChange> RemovedTypes { get; set; } = [];
    public IReadOnlyList<ApiTypeDiff> ChangedTypes { get; set; } = [];
}
