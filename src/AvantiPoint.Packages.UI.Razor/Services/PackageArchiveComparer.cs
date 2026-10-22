using System.IO.Compression;
using System.Xml.Linq;

namespace AvantiPoint.Packages.UI.Services;

public static class PackageArchiveComparer
{
    private static readonly HashSet<string> TfmRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "lib", "ref", "build", "buildTransitive", "contentFiles"
    };

    public static IReadOnlyList<PackageArchiveFile> ListFiles(Stream nupkg)
    {
        ArgumentNullException.ThrowIfNull(nupkg);
        using var zip = OpenZip(nupkg);
        return zip.Entries
            .Where(static e => !IsDirectory(e))
            .Select(static e => new PackageArchiveFile
            {
                Path = NormalizePath(e.FullName),
                Size = e.Length,
                Crc32 = e.Crc32
            })
            .OrderBy(static f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> ListAssetFolders(IEnumerable<PackageArchiveFile> files)
    {
        var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var folder = TryGetAssetFolder(file.Path);
            if (folder is not null)
            {
                folders.Add(folder);
            }
        }

        return folders.ToList();
    }

    public static IReadOnlyDictionary<string, string> ReadNuspecFields(Stream nupkg)
    {
        ArgumentNullException.ThrowIfNull(nupkg);
        using var zip = OpenZip(nupkg);
        var entry = zip.Entries.FirstOrDefault(static e =>
                e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                && !NormalizePath(e.FullName).Contains('/'))
            ?? zip.Entries.FirstOrDefault(static e =>
                e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var metadata = doc.Root?.Elements().FirstOrDefault(static x =>
            x.Name.LocalName.Equals("metadata", StringComparison.OrdinalIgnoreCase));
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (metadata is null)
        {
            return fields;
        }

        foreach (var el in metadata.Elements())
        {
            var name = el.Name.LocalName;
            if (name.Equals("dependencies", StringComparison.OrdinalIgnoreCase)
                || name.Equals("contentFiles", StringComparison.OrdinalIgnoreCase)
                || name.Equals("frameworkAssemblies", StringComparison.OrdinalIgnoreCase)
                || name.Equals("packageTypes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.Equals("repository", StringComparison.OrdinalIgnoreCase))
            {
                AddField(fields, "repository.url", (string?)el.Attribute("url"));
                AddField(fields, "repository.type", (string?)el.Attribute("type"));
                AddField(fields, "repository.commit", (string?)el.Attribute("commit"));
                continue;
            }

            AddField(fields, name, el.Value);
        }

        return fields;
    }

    public static PackageDiffResult Compare(
        string packageId,
        string currentVersion,
        Stream currentPackage,
        string? compareVersion,
        Stream? comparePackage,
        bool onlyOneVersionInFeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        ArgumentNullException.ThrowIfNull(currentPackage);

        var currentFiles = ListFiles(currentPackage);
        currentPackage.Position = 0;
        var currentFolders = ListAssetFolders(currentFiles);
        currentPackage.Position = 0;
        var currentNuspec = ReadNuspecFields(currentPackage);

        if (comparePackage is null || string.IsNullOrWhiteSpace(compareVersion))
        {
            return new PackageDiffResult
            {
                PackageId = packageId,
                CurrentVersion = currentVersion,
                CompareVersion = null,
                Message = onlyOneVersionInFeed
                    ? "Only one version in this feed"
                    : "Select another version to compare.",
                CurrentFiles = currentFiles,
                CurrentAssetFolders = currentFolders
            };
        }

        comparePackage.Position = 0;
        var otherFiles = ListFiles(comparePackage);
        comparePackage.Position = 0;
        var otherFolders = ListAssetFolders(otherFiles);
        comparePackage.Position = 0;
        var otherNuspec = ReadNuspecFields(comparePackage);

        var currentMap = currentFiles.ToDictionary(static f => f.Path, StringComparer.OrdinalIgnoreCase);
        var otherMap = otherFiles.ToDictionary(static f => f.Path, StringComparer.OrdinalIgnoreCase);
        var changes = new List<PackageFileChange>();

        foreach (var path in currentMap.Keys.Union(otherMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            currentMap.TryGetValue(path, out var newer);
            otherMap.TryGetValue(path, out var older);
            if (newer is null && older is not null)
            {
                changes.Add(new PackageFileChange
                {
                    Path = path,
                    Kind = "removed",
                    OldSize = older.Size,
                    OldCrc32 = older.Crc32
                });
            }
            else if (newer is not null && older is null)
            {
                changes.Add(new PackageFileChange
                {
                    Path = path,
                    Kind = "added",
                    NewSize = newer.Size,
                    NewCrc32 = newer.Crc32
                });
            }
            else if (newer is not null && older is not null
                     && (newer.Size != older.Size || newer.Crc32 != older.Crc32))
            {
                changes.Add(new PackageFileChange
                {
                    Path = path,
                    Kind = "changed",
                    OldSize = older.Size,
                    NewSize = newer.Size,
                    OldCrc32 = older.Crc32,
                    NewCrc32 = newer.Crc32
                });
            }
        }

        var addedFolders = currentFolders.Except(otherFolders, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var removedFolders = otherFolders.Except(currentFolders, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nuspecKeys = currentNuspec.Keys.Union(otherNuspec.Keys, StringComparer.OrdinalIgnoreCase);
        var nuspecChanges = new List<PackageNuspecChange>();
        foreach (var key in nuspecKeys.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))
        {
            currentNuspec.TryGetValue(key, out var newer);
            otherNuspec.TryGetValue(key, out var older);
            if (string.Equals(newer, older, StringComparison.Ordinal))
            {
                continue;
            }

            nuspecChanges.Add(new PackageNuspecChange
            {
                Field = key,
                OldValue = older,
                NewValue = newer
            });
        }

        return new PackageDiffResult
        {
            PackageId = packageId,
            CurrentVersion = currentVersion,
            CompareVersion = compareVersion,
            CurrentFiles = currentFiles,
            FileChanges = changes,
            CurrentAssetFolders = currentFolders,
            AddedAssetFolders = addedFolders,
            RemovedAssetFolders = removedFolders,
            NuspecChanges = nuspecChanges
        };
    }

    private static ZipArchive OpenZip(Stream nupkg)
    {
        if (!nupkg.CanSeek)
        {
            throw new InvalidOperationException("Package stream must be seekable.");
        }

        nupkg.Position = 0;
        return new ZipArchive(nupkg, ZipArchiveMode.Read, leaveOpen: true);
    }

    private static bool IsDirectory(ZipArchiveEntry entry)
        => string.IsNullOrEmpty(entry.Name)
           || entry.FullName.EndsWith('/')
           || entry.FullName.EndsWith('\\');

    private static string NormalizePath(string path)
        => path.Replace('\\', '/').TrimStart('/');

    private static string? TryGetAssetFolder(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !TfmRoots.Contains(parts[0]))
        {
            return null;
        }

        return $"{parts[0]}/{parts[1]}";
    }

    private static void AddField(IDictionary<string, string> fields, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        fields[name] = value.Trim();
    }
}
