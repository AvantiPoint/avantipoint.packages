using System.IO.Compression;
using System.Buffers.Binary;
using System.Xml;
using System.Xml.Linq;
using AvantiPoint.Packages.Registry.Native.Storage;

namespace AvantiPoint.Packages.Registry.Native.Swift;

public static class XcframeworkValidator
{
    public static void Validate(Stream stream, string module, NativeRegistryOptions options, CancellationToken cancellationToken = default)
    {
        ValidateDirectoryBounds(stream, options);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count == 0 || archive.Entries.Count > options.MaxArchiveEntries)
            throw new InvalidDataException("Invalid XCFramework entry count.");
        var root = module + ".xcframework/";
        long expanded = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var files = new HashSet<string>(StringComparer.Ordinal);
        var symlinks = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = entry.FullName;
            if (!name.StartsWith(root, StringComparison.Ordinal) || !ArtifactPath.IsValid(name.TrimEnd('/'))
                || !names.Add(name.TrimEnd('/')) || entry.Length > options.MaxExpandedArchiveBytes - expanded)
                throw new InvalidDataException("Invalid XCFramework archive layout.");
            // Central-directory sizes are untrusted. Bound actual inflation too,
            // including forged size fields and compressed metadata/symlink bombs.
            long actual = 0;
            using (var content = entry.Open())
            {
                var buffer = new byte[81920];
                int count;
                while ((count = content.Read(buffer)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (count > entry.Length - actual || count > options.MaxExpandedArchiveBytes - expanded)
                        throw new InvalidDataException("XCFramework expanded data exceeds its limits.");
                    actual += count;
                    expanded += count;
                }
            }
            if (actual != entry.Length) throw new InvalidDataException("Truncated XCFramework entry.");
            var normalized = name.TrimEnd('/');
            paths.Add(normalized);
            var parent = normalized;
            while (parent.LastIndexOf('/') is var separator && separator > 0)
            {
                parent = parent[..separator];
                paths.Add(parent);
            }
            if (new[] { ".swift", ".swiftsourceinfo", ".m", ".mm", ".c", ".cc", ".cpp", ".p12", ".p8", ".key", ".mobileprovision" }
                .Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                || name.Split('/').Any(p => p.Equals("Sources", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("XCFramework distributions must not contain implementation source.");
            // Framework symlinks must stay inside the archive. Do not extract or
            // follow them on the server. Directory entries and regular files only.
            var mode = (entry.ExternalAttributes >> 16) & 0xF000;
            if (!name.EndsWith('/') && mode != 0x4000 && entry.Length > 0) files.Add(normalized);
            if (mode != 0 && mode != 0x8000 && mode != 0x4000 && mode != 0xA000)
                throw new InvalidDataException("Unsupported archive entry type.");
            if (mode == 0xA000)
            {
                if (entry.Length > 1024) throw new InvalidDataException("Invalid framework symlink.");
                using var reader = new StreamReader(entry.Open());
                var target = reader.ReadToEnd();
                if (!ArtifactPath.IsValid(target)) throw new InvalidDataException("Unsafe framework symlink.");
                symlinks.Add(normalized, target);
            }
        }
        string Resolve(string path)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                var changed = false;
                var parts = path.Split('/');
                for (var i = 1; i <= parts.Length; i++)
                {
                    var prefix = string.Join('/', parts[..i]);
                    if (!symlinks.TryGetValue(prefix, out var target)) continue;
                    if (!visited.Add(prefix)) throw new InvalidDataException("Cyclic framework symlink.");
                    var separator = prefix.LastIndexOf('/');
                    path = prefix[..(separator + 1)] + target
                        + (i == parts.Length ? "" : "/" + string.Join('/', parts[i..]));
                    if (path.Length > 1024 || !path.StartsWith(root, StringComparison.Ordinal))
                        throw new InvalidDataException("Unsafe framework symlink target.");
                    changed = true;
                    break;
                }
                if (changed) continue;
                if (!paths.Contains(path)) throw new InvalidDataException("Dangling framework symlink.");
                return path;
            }
        }
        foreach (var link in symlinks.Keys) Resolve(link);
        var info = archive.GetEntry(root + "Info.plist") ?? throw new InvalidDataException("Missing XCFramework Info.plist.");
        if (info.Length > 1024 * 1024) throw new InvalidDataException("XCFramework Info.plist is too large.");
        using var plistStream = info.Open();
        using var xml = XmlReader.Create(plistStream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024,
        });
        var plist = XDocument.Load(xml);
        var dictionary = plist.Root?.Element("dict");
        var libraries = Value(dictionary, "AvailableLibraries")?.Elements("dict").ToArray();
        if (libraries is null || libraries.Length == 0) throw new InvalidDataException("Missing XCFramework slices.");
        foreach (var library in libraries)
        {
            var identifier = Value(library, "LibraryIdentifier")?.Value;
            var libraryPath = Value(library, "LibraryPath")?.Value;
            if (!ArtifactPath.IsValidSegment(identifier) || libraryPath != module + ".framework"
                || Value(library, "SupportedPlatform")?.Value is not ("ios" or "macos" or "tvos" or "watchos" or "xros")
                || Value(library, "SupportedArchitectures")?.Elements("string").Any() != true)
                throw new InvalidDataException("Invalid XCFramework slice metadata.");
            var framework = root + identifier + "/" + libraryPath + "/";
            if (!files.Contains(Resolve(framework + module))
                || !files.Any(n => n.StartsWith(framework, StringComparison.Ordinal) && n.EndsWith(".swiftinterface", StringComparison.Ordinal)
                    && !n.EndsWith(".private.swiftinterface", StringComparison.Ordinal)))
                throw new InvalidDataException("Every Swift slice must include its framework binary and public Swift interface.");
        }
    }

    private static void ValidateDirectoryBounds(Stream stream, NativeRegistryOptions options)
    {
        // Validate the small ZIP footer before ZipArchive allocates its entry list.
        // ZIP64 is deliberately excluded from this first bounded SDK-archive path.
        if (!stream.CanSeek || stream.Length < 22) throw new InvalidDataException("Invalid ZIP stream.");
        var tail = new byte[(int)Math.Min(stream.Length, 65557)];
        stream.Position = stream.Length - tail.Length;
        stream.ReadExactly(tail);
        for (var i = tail.Length - 22; i >= 0; i--)
        {
            var footer = tail.AsSpan(i);
            if (BinaryPrimitives.ReadUInt32LittleEndian(footer) != 0x06054b50
                || i + 22 + BinaryPrimitives.ReadUInt16LittleEndian(footer[20..]) != tail.Length) continue;
            var entries = BinaryPrimitives.ReadUInt16LittleEndian(footer[10..]);
            var directorySize = BinaryPrimitives.ReadUInt32LittleEndian(footer[12..]);
            var directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(footer[16..]);
            if (BinaryPrimitives.ReadUInt16LittleEndian(footer[4..]) != 0
                || BinaryPrimitives.ReadUInt16LittleEndian(footer[6..]) != 0
                || BinaryPrimitives.ReadUInt16LittleEndian(footer[8..]) != entries
                || entries == ushort.MaxValue || entries > options.MaxArchiveEntries
                || directorySize > 16 * 1024 * 1024 || directoryOffset == uint.MaxValue
                || (long)directoryOffset + directorySize > stream.Length - tail.Length + i)
                throw new InvalidDataException("Unsupported or oversized ZIP directory.");
            stream.Position = 0;
            return;
        }
        throw new InvalidDataException("Missing ZIP directory footer.");
    }

    private static XElement? Value(XElement? dictionary, string key) => dictionary?.Elements("key")
        .FirstOrDefault(k => k.Value == key)?.ElementsAfterSelf().FirstOrDefault();
}
