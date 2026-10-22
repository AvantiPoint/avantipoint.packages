using System.IO.Compression;
using AvantiPoint.Packages.Core;
using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Services;

public sealed class PackageDiffService(
    IPackageStorageService storage,
    ISourceLinkContentFetcher sourceLinkFetcher) : IPackageDiffService
{
    public async Task<PackageDiffResult> GetDiffAsync(
        string packageId,
        string currentVersion,
        string? compareVersion,
        bool onlyOneVersionInFeed,
        string? filePath = null,
        string? view = null,
        string? tfm = null,
        CancellationToken cancellationToken = default)
    {
        var viewMode = string.Equals(view, "split", StringComparison.OrdinalIgnoreCase) ? "split" : "unified";
        if (!NuGetVersion.TryParse(currentVersion, out var currentNuGetVersion))
        {
            return new PackageDiffResult
            {
                PackageId = packageId,
                CurrentVersion = currentVersion,
                Error = $"Invalid version '{currentVersion}'.",
                View = viewMode
            };
        }

        MemoryStream? currentBuffer = null;
        MemoryStream? compareBuffer = null;
        try
        {
            currentBuffer = await BufferPackageAsync(packageId, currentNuGetVersion, cancellationToken);
            if (currentBuffer is null)
            {
                return new PackageDiffResult
                {
                    PackageId = packageId,
                    CurrentVersion = currentVersion,
                    CompareVersion = compareVersion,
                    Error = "Package content was not found in storage.",
                    Message = onlyOneVersionInFeed ? "Only one version in this feed" : null,
                    View = viewMode
                };
            }

            if (!string.IsNullOrWhiteSpace(compareVersion)
                && !string.Equals(compareVersion, currentVersion, StringComparison.OrdinalIgnoreCase)
                && NuGetVersion.TryParse(compareVersion, out var compareNuGetVersion))
            {
                compareBuffer = await BufferPackageAsync(packageId, compareNuGetVersion, cancellationToken);
            }

            currentBuffer.Position = 0;
            var archiveCompare = PackageArchiveComparer.Compare(
                packageId,
                currentVersion,
                currentBuffer,
                compareBuffer is null ? null : compareVersion,
                compareBuffer,
                onlyOneVersionInFeed);

            currentBuffer.Position = 0;
            using var currentZip = new ZipArchive(currentBuffer, ZipArchiveMode.Read, leaveOpen: true);
            ZipArchive? compareZip = null;
            if (compareBuffer is not null)
            {
                compareBuffer.Position = 0;
                compareZip = new ZipArchive(compareBuffer, ZipArchiveMode.Read, leaveOpen: true);
            }

            try
            {
                var tree = BuildTree(currentZip, compareZip, tfm, archiveCompare.FileChanges);
                var selected = ChooseFile(filePath, tree);
                string? leftText = null;
                string? rightText = null;
                string? sourceKind = null;
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    if (compareZip is not null)
                    {
                        var left = await PackageSourceText.ReadAsync(compareZip, selected, sourceLinkFetcher, cancellationToken);
                        leftText = left.Text;
                        sourceKind = left.Kind;
                    }

                    var right = await PackageSourceText.ReadAsync(currentZip, selected, sourceLinkFetcher, cancellationToken);
                    rightText = right.Text;
                    sourceKind = right.Kind == "Missing" ? sourceKind : right.Kind;
                }

                IReadOnlyList<DiffLine> lines = [];
                if (archiveCompare.HasCompareVersion && selected is not null)
                {
                    lines = PackageTextDiffer.Unified(leftText, rightText);
                }

                if (string.IsNullOrWhiteSpace(filePath) && archiveCompare.HasCompareVersion && !HasHunks(lines))
                {
                    foreach (var candidate in CandidateFiles(null, tree).Skip(1).Take(12))
                    {
                        selected = candidate;
                        leftText = null;
                        rightText = null;
                        sourceKind = null;
                        if (compareZip is not null)
                        {
                            var left = await PackageSourceText.ReadAsync(compareZip, selected, sourceLinkFetcher, cancellationToken);
                            leftText = left.Text;
                            sourceKind = left.Kind;
                        }

                        var probed = await PackageSourceText.ReadAsync(currentZip, selected, sourceLinkFetcher, cancellationToken);
                        rightText = probed.Text;
                        sourceKind = probed.Kind == "Missing" ? sourceKind : probed.Kind;
                        lines = PackageTextDiffer.Unified(leftText, rightText);
                        if (HasHunks(lines))
                        {
                            break;
                        }
                    }
                }

                var result = new PackageDiffResult
                {
                    PackageId = archiveCompare.PackageId,
                    CurrentVersion = archiveCompare.CurrentVersion,
                    CompareVersion = archiveCompare.CompareVersion,
                    Message = archiveCompare.Message,
                    Error = archiveCompare.Error,
                    CurrentFiles = archiveCompare.CurrentFiles,
                    FileChanges = archiveCompare.FileChanges,
                    CurrentAssetFolders = archiveCompare.CurrentAssetFolders,
                    AddedAssetFolders = archiveCompare.AddedAssetFolders,
                    RemovedAssetFolders = archiveCompare.RemovedAssetFolders,
                    NuspecChanges = archiveCompare.NuspecChanges,
                    Tree = tree,
                    SelectedFile = selected,
                    LeftText = leftText,
                    RightText = rightText,
                    TextSource = sourceKind,
                    Lines = lines,
                    View = viewMode
                };
                currentBuffer.Position = 0;
                if (compareBuffer is not null) { compareBuffer.Position = 0; }
                // PackageDecompileComparer.Fill is a no-op leftover; Files tree + text hunks are already on result.
                return result;
            }
            finally
            {
                compareZip?.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new PackageDiffResult
            {
                PackageId = packageId,
                CurrentVersion = currentVersion,
                CompareVersion = compareVersion,
                Error = $"Could not compare packages: {ex.Message}",
                Message = onlyOneVersionInFeed ? "Only one version in this feed" : null,
                View = viewMode
            };
        }
        finally
        {
            currentBuffer?.Dispose();
            compareBuffer?.Dispose();
        }
    }

    private static IReadOnlyList<PackageDiffTreeNode> BuildTree(
        ZipArchive current,
        ZipArchive? other,
        string? tfm,
        IReadOnlyList<PackageFileChange> fileChanges)
    {
        var currentFiles = current.Entries
            .Where(static e => !string.IsNullOrEmpty(e.Name))
            .Select(static e => PackageSourceText.Normalize(e.FullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var otherFiles = other?.Entries
            .Where(static e => !string.IsNullOrEmpty(e.Name))
            .Select(static e => PackageSourceText.Normalize(e.FullName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var changeMap = fileChanges.ToDictionary(static c => c.Path, static c => c.Kind, StringComparer.OrdinalIgnoreCase);

        var paths = currentFiles.Union(otherFiles, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(tfm))
        {
            var needle = "/" + tfm.Trim() + "/";
            var filtered = paths.Where(p => p.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count > 0)
            {
                paths = filtered;
            }
        }

        var roots = new List<PackageDiffTreeNode>();
        foreach (var path in paths)
        {
            var change = ChangeOf(path, currentFiles, otherFiles, changeMap);
            var children = new List<PackageDiffTreeNode>();
            if (PackageSourceText.IsAssemblyPath(path))
            {
                var zip = currentFiles.Contains(path) ? current : other!;
                var bytes = PackageSourceText.ReadZipBytes(zip, path);
                if (bytes is not null)
                {
                    foreach (var type in PackageSourceText.ListTypes(bytes))
                    {
                        var typePath = PackageSourceText.TypeFilePath(path, type);
                        children.Add(new PackageDiffTreeNode
                        {
                            Path = typePath,
                            Name = type + ".cs",
                            Kind = "type",
                            Change = change == "same" ? "same" : change
                        });
                    }
                }
            }

            roots.Add(new PackageDiffTreeNode
            {
                Path = path,
                Name = path,
                Kind = PackageSourceText.IsAssemblyPath(path) ? "assembly" : "file",
                Change = change,
                Children = children
            });
        }

        return roots;
    }

    private static string ChangeOf(
        string path,
        HashSet<string> current,
        HashSet<string> other,
        IReadOnlyDictionary<string, string> changeMap)
    {
        var inCurrent = current.Contains(path);
        var inOther = other.Contains(path);
        if (inCurrent && !inOther)
        {
            return "added";
        }

        if (!inCurrent && inOther)
        {
            return "removed";
        }

        return changeMap.TryGetValue(path, out var kind) ? kind : "same";
    }

    private static bool HasHunks(IReadOnlyList<DiffLine> lines)
        => lines.Any(static l => l.Kind is "add" or "del");

    private static bool IsPortableLib(string path)
        => path.Contains("/lib/net", StringComparison.OrdinalIgnoreCase)
           && !path.Contains("-android", StringComparison.OrdinalIgnoreCase)
           && !path.Contains("-ios", StringComparison.OrdinalIgnoreCase)
           && !path.Contains("-maccatalyst", StringComparison.OrdinalIgnoreCase)
           && !path.Contains("-macos", StringComparison.OrdinalIgnoreCase)
           && !path.Contains("-tvos", StringComparison.OrdinalIgnoreCase)
           && !path.Contains("-windows", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> CandidateFiles(string? requested, IReadOnlyList<PackageDiffTreeNode> tree)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return [requested];
        }

        var ordered = new List<string>();
        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path)
                && !ordered.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(path);
            }
        }

        static bool IsNoisyType(string path)
            => path.Contains("BuildMetadata", StringComparison.OrdinalIgnoreCase)
               || path.Contains("AssemblyInfo", StringComparison.OrdinalIgnoreCase)
               || path.Contains("EmbeddedAttribute", StringComparison.OrdinalIgnoreCase)
               || path.Contains("RefSafetyRules", StringComparison.OrdinalIgnoreCase);

        foreach (var node in tree.Where(static n =>
            n.Path.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)))
        {
            Add(node.Path);
        }

        foreach (var node in tree.Where(static n => n.Kind == "assembly" && n.Change == "changed" && IsPortableLib(n.Path)))
        {
            foreach (var child in node.Children.Where(c => !IsNoisyType(c.Path)))
            {
                Add(child.Path);
            }
        }

        foreach (var node in tree.Where(static n => n.Kind == "file" && n.Change is "changed" or "added" or "removed" && PackageSourceText.IsTextPath(n.Path)))
        {
            Add(node.Path);
        }

        foreach (var node in tree.Where(static n => n.Kind == "assembly" && n.Change == "changed"))
        {
            foreach (var child in node.Children)
            {
                Add(child.Path);
            }
        }

        foreach (var node in tree.Where(static n => n.Kind == "assembly" && IsPortableLib(n.Path)))
        {
            foreach (var child in node.Children)
            {
                Add(child.Path);
            }
        }

        foreach (var node in tree)
        {
            foreach (var child in node.Children)
            {
                Add(child.Path);
            }

            if (node.Kind == "file")
            {
                Add(node.Path);
            }
        }

        return ordered;
    }

    private static string? ChooseFile(string? requested, IReadOnlyList<PackageDiffTreeNode> tree)
        => CandidateFiles(requested, tree).FirstOrDefault();

    private async Task<MemoryStream?> BufferPackageAsync(
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await storage.GetPackageStreamAsync(packageId, version, cancellationToken);
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return buffer;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
