using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using AvantiPoint.Packages.Registry.Native.Storage;

namespace AvantiPoint.Packages.Registry.Native.Swift;

public static class XcframeworkValidator
{
    public static void Validate(Stream stream, string module, NativeRegistryOptions options)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count == 0 || archive.Entries.Count > options.MaxArchiveEntries)
            throw new InvalidDataException("Invalid XCFramework entry count.");
        var root = module + ".xcframework/";
        long expanded = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (!name.StartsWith(root, StringComparison.Ordinal) || !ArtifactPath.IsValid(name.TrimEnd('/'))
                || !names.Add(name.TrimEnd('/')) || entry.Length > options.MaxExpandedArchiveBytes - expanded)
                throw new InvalidDataException("Invalid XCFramework archive layout.");
            expanded += entry.Length;
            if (new[] { ".swift", ".m", ".mm", ".c", ".cc", ".cpp", ".p12", ".p8", ".key", ".mobileprovision" }
                .Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                || name.Split('/').Any(p => p.Equals("Sources", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("XCFramework distributions must not contain implementation source.");
            // Framework symlinks must stay inside the archive. Do not extract or
            // follow them on the server. Directory entries and regular files only.
            var mode = (entry.ExternalAttributes >> 16) & 0xF000;
            if (mode != 0 && mode != 0x8000 && mode != 0x4000 && mode != 0xA000)
                throw new InvalidDataException("Unsupported archive entry type.");
            if (mode == 0xA000)
            {
                if (entry.Length > 1024) throw new InvalidDataException("Invalid framework symlink.");
                using var reader = new StreamReader(entry.Open());
                var target = reader.ReadToEnd();
                if (!ArtifactPath.IsValid(target)) throw new InvalidDataException("Unsafe framework symlink.");
            }
        }
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
            if (!names.Contains(framework + module)
                || !names.Any(n => n.StartsWith(framework, StringComparison.Ordinal) && n.EndsWith(".swiftinterface", StringComparison.Ordinal)
                    && !n.EndsWith(".private.swiftinterface", StringComparison.Ordinal)))
                throw new InvalidDataException("Every Swift slice must include its framework binary and public Swift interface.");
        }
    }

    private static XElement? Value(XElement? dictionary, string key) => dictionary?.Elements("key")
        .FirstOrDefault(k => k.Value == key)?.ElementsAfterSelf().FirstOrDefault();
}
