using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Compares npm metadata and stored tgz content without extracting or executing package files.
/// All archive, JSON, path, text, and diff work is bounded because package content is untrusted.
/// </summary>
public static class NpmPackageComparisonBuilder
{
    public const long MaximumCompressedTarballBytes = 32 * 1024 * 1024;
    public const long MaximumExpandedTarballBytes = 64 * 1024 * 1024;
    public const long MaximumSingleFileBytes = 16 * 1024 * 1024;
    public const int MaximumArchiveEntries = 4096;
    public const int MaximumTextPreviewBytes = 256 * 1024;

    private const int MaximumMetadataBytes = 1024 * 1024;
    private const int MaximumMetadataFields = 512;
    private const int MaximumMetadataDepth = 16;
    private const int MaximumMetadataValueCharacters = 2048;
    private const int MaximumMetadataPathCharacters = 1024;
    private const int MaximumDiffLinesPerSide = 1500;
    private const int MaximumDiffPreviewLines = 600;
    private const int DiffContextLines = 3;
    private const int MaximumPathCharacters = 1024;
    private const int MaximumPathSegments = 64;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cjs", ".css", ".cts", ".graphql", ".gql", ".hbs", ".html", ".js", ".json",
        ".jsx", ".less", ".md", ".mjs", ".mts", ".npmignore", ".npmrc", ".scss", ".sh",
        ".svg", ".text", ".toml", ".ts", ".tsx", ".txt", ".xml", ".yaml", ".yml",
    };

    private static readonly HashSet<string> TextFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LICENSE", "LICENCE", "NOTICE", "README", "CHANGELOG", "HISTORY", "package.json",
    };

    private static readonly HashSet<string> IgnoredMetadataProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "_attachments", "readme",
    };

    public static async Task<NpmPackageVersionComparison> CompareAsync(
        string packageName,
        string baseVersion,
        string baseMetadataJson,
        Stream? baseTarball,
        string targetVersion,
        string targetMetadataJson,
        Stream? targetTarball,
        string? selectedFile = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetVersion);

        var metadata = CompareMetadata(baseMetadataJson, targetMetadataJson);
        var baseArchive = baseTarball is null
            ? TarballReadResult.Missing
            : await ReadTarballAsync(baseTarball, cancellationToken);
        var targetArchive = targetTarball is null
            ? TarballReadResult.Missing
            : await ReadTarballAsync(targetTarball, cancellationToken);

        var archiveComparison = CompareArchives(baseArchive, targetArchive, selectedFile);
        return new NpmPackageVersionComparison(
            packageName,
            baseVersion,
            targetVersion,
            metadata.Changes,
            metadata.UnavailableReason,
            archiveComparison.FileChanges,
            archiveComparison.FileTree,
            archiveComparison.State,
            archiveComparison.Message,
            archiveComparison.SelectedFile,
            archiveComparison.SelectedFileDiff,
            archiveComparison.SelectedFileUnavailableReason);
    }

    private static MetadataComparison CompareMetadata(string? baseJson, string? targetJson)
    {
        var baseRead = ReadMetadata(baseJson);
        var targetRead = ReadMetadata(targetJson);
        if (baseRead.Error is not null || targetRead.Error is not null)
        {
            var reason = baseRead.Error is not null && targetRead.Error is not null
                ? "Published metadata for both versions is unavailable or exceeds the inspection limit."
                : baseRead.Error is not null
                    ? "Published metadata for the comparison version is unavailable or exceeds the inspection limit."
                    : "Published metadata for the selected version is unavailable or exceeds the inspection limit.";
            return new MetadataComparison([], reason);
        }

        var fields = baseRead.Fields.Keys
            .Union(targetRead.Fields.Keys, StringComparer.Ordinal)
            .OrderBy(static field => field, StringComparer.Ordinal)
            .ToArray();
        var changes = new List<NpmPackageMetadataChange>();
        foreach (var field in fields)
        {
            baseRead.Fields.TryGetValue(field, out var baseValue);
            targetRead.Fields.TryGetValue(field, out var targetValue);
            if (!string.Equals(baseValue, targetValue, StringComparison.Ordinal))
            {
                changes.Add(new NpmPackageMetadataChange(field, baseValue, targetValue));
            }
        }

        return new MetadataComparison(changes, null);
    }

    private static MetadataReadResult ReadMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)
            || Encoding.UTF8.GetByteCount(json) > MaximumMetadataBytes)
        {
            return MetadataReadResult.Invalid;
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = MaximumMetadataDepth + 2,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return MetadataReadResult.Invalid;
            }

            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!FlattenMetadata(document.RootElement, string.Empty, 0, fields))
            {
                return MetadataReadResult.Invalid;
            }

            return new MetadataReadResult(fields, null);
        }
        catch (JsonException)
        {
            return MetadataReadResult.Invalid;
        }
    }

    private static bool FlattenMetadata(
        JsonElement element,
        string path,
        int depth,
        IDictionary<string, string> fields)
    {
        if (depth > MaximumMetadataDepth || fields.Count > MaximumMetadataFields)
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
            {
                if (depth == 0 && IgnoredMetadataProperties.Contains(property.Name))
                {
                    continue;
                }

                var childPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
                if (childPath.Length > MaximumMetadataPathCharacters)
                {
                    return false;
                }

                if (!FlattenMetadata(property.Value, childPath, depth + 1, fields))
                {
                    return false;
                }
            }

            return fields.Count <= MaximumMetadataFields;
        }

        var value = element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();
        if (value.Length > MaximumMetadataValueCharacters)
        {
            value = value[..MaximumMetadataValueCharacters] + "… (truncated)";
        }

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (fields.ContainsKey(path))
        {
            return false;
        }

        fields.Add(path, value);
        return fields.Count <= MaximumMetadataFields;
    }

    private static async Task<TarballReadResult> ReadTarballAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        try
        {
            if (source.CanSeek)
            {
                if (source.Length > MaximumCompressedTarballBytes)
                {
                    return TarballReadResult.Invalid("The stored tarball exceeds the compressed inspection limit.");
                }

                source.Position = 0;
            }

            using var compressed = new SizeLimitedReadStream(source, MaximumCompressedTarballBytes, leaveOpen: true);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: true);
            using var expanded = new SizeLimitedReadStream(gzip, MaximumExpandedTarballBytes, leaveOpen: false);
            using var tar = new TarReader(expanded, leaveOpen: true);
            var files = new Dictionary<string, InspectedFile>(StringComparer.Ordinal);
            long declaredBytes = 0;
            var entryCount = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = tar.GetNextEntry(copyData: false);
                if (entry is null)
                {
                    break;
                }

                entryCount++;
                if (entryCount > MaximumArchiveEntries)
                {
                    return TarballReadResult.Invalid("The stored tarball contains too many entries to inspect safely.");
                }

                var path = NormalizePath(entry.Name);
                if (path is null)
                {
                    return TarballReadResult.Invalid("The stored tarball contains an unsafe or invalid path.");
                }

                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    continue;
                }

                if (entry.DataStream is null || entry.Length < 0 || entry.Length > MaximumSingleFileBytes)
                {
                    return TarballReadResult.Invalid("A stored package file exceeds the per-file inspection limit.");
                }

                declaredBytes += entry.Length;
                if (declaredBytes > MaximumExpandedTarballBytes)
                {
                    return TarballReadResult.Invalid("The stored tarball exceeds the expanded inspection limit.");
                }

                if (files.ContainsKey(path))
                {
                    return TarballReadResult.Invalid("The stored tarball contains duplicate normalized paths.");
                }

                var inspected = await ReadFileAsync(path, entry.Length, entry.DataStream, cancellationToken);
                files.Add(path, inspected);
            }

            return new TarballReadResult(files, null, false);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException)
        {
            return TarballReadResult.Invalid("The stored tarball is invalid or exceeds an inspection limit.");
        }
    }

    private static async Task<InspectedFile> ReadFileAsync(
        string path,
        long length,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (length <= MaximumTextPreviewBytes && IsPotentialTextFile(path))
        {
            var bytes = new byte[(int)length];
            var offset = 0;
            while (offset < bytes.Length)
            {
                var read = await content.ReadAsync(bytes.AsMemory(offset), cancellationToken);
                if (read == 0)
                {
                    throw new InvalidDataException("Tar entry ended before its declared size.");
                }

                offset += read;
            }

            var digest = Convert.ToHexString(SHA256.HashData(bytes));
            try
            {
                var text = StrictUtf8.GetString(bytes).TrimStart('\uFEFF');
                if (text.IndexOf('\0') < 0)
                {
                    return new InspectedFile(path, length, digest, text, null);
                }
            }
            catch (DecoderFallbackException)
            {
            }

            return new InspectedFile(path, length, digest, null, "This package file is not valid UTF-8 text.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[16 * 1024];
        long remaining = length;
        while (remaining > 0)
        {
            var read = await content.ReadAsync(
                buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                cancellationToken);
            if (read == 0)
            {
                throw new InvalidDataException("Tar entry ended before its declared size.");
            }

            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }

        var unavailable = length > MaximumTextPreviewBytes && IsPotentialTextFile(path)
            ? "This text file is too large for a safe inline diff."
            : "This package file is binary or has an unsupported text format.";
        return new InspectedFile(path, length, Convert.ToHexString(hash.GetHashAndReset()), null, unavailable);
    }

    private static ArchiveComparison CompareArchives(
        TarballReadResult baseArchive,
        TarballReadResult targetArchive,
        string? selectedFile)
    {
        if (!baseArchive.IsAvailable || !targetArchive.IsAvailable)
        {
            var unavailableMessage = BuildArchiveUnavailableMessage(baseArchive, targetArchive);
            return new ArchiveComparison([], [], "unavailable", unavailableMessage, null, [], null);
        }

        var changes = new List<NpmPackageFileChange>();
        foreach (var path in baseArchive.Files.Keys
                     .Union(targetArchive.Files.Keys, StringComparer.Ordinal)
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            baseArchive.Files.TryGetValue(path, out var baseFile);
            targetArchive.Files.TryGetValue(path, out var targetFile);
            if (baseFile is not null
                && targetFile is not null
                && baseFile.Size == targetFile.Size
                && string.Equals(baseFile.Sha256, targetFile.Sha256, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(new NpmPackageFileChange(
                path,
                baseFile is null ? "added" : targetFile is null ? "removed" : "changed",
                baseFile?.Size,
                targetFile?.Size,
                (baseFile is null || baseFile.Text is not null)
                && (targetFile is null || targetFile.Text is not null)));
        }

        var selectedChange = string.IsNullOrWhiteSpace(selectedFile)
            ? null
            : changes.FirstOrDefault(change => string.Equals(change.Path, selectedFile, StringComparison.Ordinal));
        var filePreview = selectedChange is null
            ? SelectedFileComparison.Empty
            : CompareSelectedFile(
                selectedChange.Path,
                baseArchive.Files.GetValueOrDefault(selectedChange.Path),
                targetArchive.Files.GetValueOrDefault(selectedChange.Path));
        var message = changes.Count == 0
            ? "The stored tarball contents are identical."
            : "File changes come from locally stored tarballs; package content is never executed.";

        return new ArchiveComparison(
            changes,
            BuildTree(changes),
            "available",
            message,
            selectedChange?.Path,
            filePreview.Lines,
            filePreview.UnavailableReason);
    }

    private static string BuildArchiveUnavailableMessage(
        TarballReadResult baseArchive,
        TarballReadResult targetArchive)
    {
        if (baseArchive.Error is not null || targetArchive.Error is not null)
        {
            return baseArchive.Error ?? targetArchive.Error!;
        }

        if (baseArchive.IsNotStored && targetArchive.IsNotStored)
        {
            return "Neither tarball is stored on this feed, so file comparison is unavailable. This page does not download proxy-only or uncached content from upstream.";
        }

        return baseArchive.IsNotStored
            ? "The comparison-version tarball is not stored on this feed, so file comparison is unavailable. This page does not fetch it from upstream."
            : "The selected-version tarball is not stored on this feed, so file comparison is unavailable. This page does not fetch it from upstream.";
    }

    private static SelectedFileComparison CompareSelectedFile(
        string path,
        InspectedFile? baseFile,
        InspectedFile? targetFile)
    {
        if ((baseFile is not null && baseFile.Text is null)
            || (targetFile is not null && targetFile.Text is null))
        {
            return new SelectedFileComparison(
                [],
                targetFile?.TextUnavailableReason
                ?? baseFile?.TextUnavailableReason
                ?? "This package file cannot be previewed as text.");
        }

        var baseText = baseFile?.Text;
        var targetText = targetFile?.Text;
        if (CountLines(baseText) > MaximumDiffLinesPerSide
            || CountLines(targetText) > MaximumDiffLinesPerSide)
        {
            return new SelectedFileComparison(
                [],
                $"'{path}' changed, but it contains too many lines for a safe inline diff.");
        }

        return new SelectedFileComparison(
            CompactDiff(PackageTextDiffer.Unified(baseText, targetText)),
            null);
    }

    private static IReadOnlyList<NpmPackageFileTreeNode> BuildTree(
        IReadOnlyList<NpmPackageFileChange> changes)
    {
        var root = new MutableTreeNode(string.Empty, string.Empty, true);
        foreach (var change in changes)
        {
            var segments = change.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var parent = root;
            var path = string.Empty;
            for (var index = 0; index < segments.Length; index++)
            {
                path = string.IsNullOrEmpty(path) ? segments[index] : $"{path}/{segments[index]}";
                var isDirectory = index < segments.Length - 1;
                parent = parent.GetOrAdd(segments[index], path, isDirectory);
                if (!isDirectory)
                {
                    parent.ChangeKind = change.ChangeKind;
                    parent.HasTextPreview = change.HasTextPreview;
                }
            }
        }

        return root.Children.Values
            .Select(ToImmutableTree)
            .OrderByDescending(static node => node.IsDirectory)
            .ThenBy(static node => node.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static NpmPackageFileTreeNode ToImmutableTree(MutableTreeNode node)
    {
        var children = node.Children.Values
            .Select(ToImmutableTree)
            .OrderByDescending(static child => child.IsDirectory)
            .ThenBy(static child => child.Name, StringComparer.Ordinal)
            .ToArray();
        var changeKind = node.IsDirectory
            ? GetDirectoryChangeKind(children)
            : node.ChangeKind;
        return new NpmPackageFileTreeNode(
            node.Name,
            node.Path,
            node.IsDirectory,
            changeKind,
            node.HasTextPreview,
            children);
    }

    private static string GetDirectoryChangeKind(IReadOnlyList<NpmPackageFileTreeNode> children)
    {
        var kinds = children.Select(static child => child.ChangeKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 1 ? kinds[0] : "changed";
    }

    private static IReadOnlyList<DiffLine> CompactDiff(IReadOnlyList<DiffLine> lines)
    {
        if (lines.Count == 0 || lines.All(static line => line.Kind == "eq"))
        {
            return lines;
        }

        var included = new bool[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Kind == "eq")
            {
                continue;
            }

            for (var context = Math.Max(0, index - DiffContextLines);
                 context <= Math.Min(lines.Count - 1, index + DiffContextLines);
                 context++)
            {
                included[context] = true;
            }
        }

        var result = new List<DiffLine>();
        var previous = -2;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!included[index])
            {
                continue;
            }

            if (index > previous + 1)
            {
                result.Add(new DiffLine
                {
                    Kind = "skip",
                    Text = "… unchanged lines …",
                });
            }

            result.Add(lines[index]);
            previous = index;
            if (result.Count >= MaximumDiffPreviewLines)
            {
                result.Add(new DiffLine
                {
                    Kind = "skip",
                    Text = "… diff preview truncated …",
                });
                break;
            }
        }

        return result;
    }

    private static int CountLines(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var lines = 1;
        foreach (var character in value)
        {
            if (character == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static bool IsPotentialTextFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return TextFileNames.Contains(fileName)
            || TextExtensions.Contains(Path.GetExtension(fileName));
    }

    private static string? NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaximumPathCharacters
            || value.IndexOf('\0') >= 0
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith('\\'))
        {
            return null;
        }

        var normalized = value.Replace('\\', '/').TrimEnd('/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        var segments = normalized.Split('/');
        if (segments.Length > MaximumPathSegments
            || segments.Any(static segment =>
                string.IsNullOrEmpty(segment)
                || segment is "." or ".."
                || segment.Any(char.IsControl)))
        {
            return null;
        }

        if (segments.Length > 1 && segments[0].Equals("package", StringComparison.Ordinal))
        {
            segments = segments[1..];
        }

        return segments.Length == 0 ? null : string.Join('/', segments);
    }

    private sealed record MetadataComparison(
        IReadOnlyList<NpmPackageMetadataChange> Changes,
        string? UnavailableReason);

    private sealed record MetadataReadResult(
        IReadOnlyDictionary<string, string> Fields,
        string? Error)
    {
        public static MetadataReadResult Invalid { get; } = new(
            new Dictionary<string, string>(StringComparer.Ordinal),
            "invalid");
    }

    private sealed record InspectedFile(
        string Path,
        long Size,
        string Sha256,
        string? Text,
        string? TextUnavailableReason);

    private sealed record TarballReadResult(
        IReadOnlyDictionary<string, InspectedFile> Files,
        string? Error,
        bool IsNotStored)
    {
        public bool IsAvailable => Error is null && !IsNotStored;

        public static TarballReadResult Missing { get; } = new(
            new Dictionary<string, InspectedFile>(StringComparer.Ordinal),
            null,
            true);

        public static TarballReadResult Invalid(string error) => new(
            new Dictionary<string, InspectedFile>(StringComparer.Ordinal),
            error,
            false);
    }

    private sealed record ArchiveComparison(
        IReadOnlyList<NpmPackageFileChange> FileChanges,
        IReadOnlyList<NpmPackageFileTreeNode> FileTree,
        string State,
        string Message,
        string? SelectedFile,
        IReadOnlyList<DiffLine> SelectedFileDiff,
        string? SelectedFileUnavailableReason);

    private sealed record SelectedFileComparison(
        IReadOnlyList<DiffLine> Lines,
        string? UnavailableReason)
    {
        public static SelectedFileComparison Empty { get; } = new([], null);
    }

    private sealed class MutableTreeNode(string name, string path, bool isDirectory)
    {
        public string Name { get; } = name;

        public string Path { get; } = path;

        public bool IsDirectory { get; } = isDirectory;

        public string ChangeKind { get; set; } = "changed";

        public bool HasTextPreview { get; set; }

        public SortedDictionary<string, MutableTreeNode> Children { get; } = new(StringComparer.Ordinal);

        public MutableTreeNode GetOrAdd(string childName, string childPath, bool childIsDirectory)
        {
            if (!Children.TryGetValue(childName, out var child))
            {
                child = new MutableTreeNode(childName, childPath, childIsDirectory);
                Children.Add(childName, child);
            }

            return child;
        }
    }

    private sealed class SizeLimitedReadStream(
        Stream inner,
        long maximumBytes,
        bool leaveOpen) : Stream
    {
        private long _bytesRead;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Count(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        public override int ReadByte() => CountByte(inner.ReadByte());

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            Count(await inner.ReadAsync(buffer, offset, count, cancellationToken));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Count(int read)
        {
            _bytesRead += read;
            if (_bytesRead > maximumBytes)
            {
                throw new InvalidDataException("Stream exceeds the npm package inspection limit.");
            }

            return read;
        }

        private int CountByte(int value)
        {
            if (value >= 0)
            {
                Count(1);
            }

            return value;
        }
    }
}
