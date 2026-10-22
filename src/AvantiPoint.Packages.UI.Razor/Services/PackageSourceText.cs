using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace AvantiPoint.Packages.UI.Services;

public static class PackageSourceText
{
    private static readonly Guid SourceLinkGuid = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
    private static readonly Guid EmbeddedSourceGuid = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".fs", ".vb", ".xml", ".nuspec", ".json", ".md", ".txt", ".props", ".targets",
        ".csproj", ".fsproj", ".vbproj", ".xaml", ".resx", ".config", ".ps1", ".js", ".ts",
        ".css", ".html", ".razor", ".yml", ".yaml", ".rel", ".clixml", ".dotnettools"
    };

    public static bool IsTextPath(string path)
        => TextExtensions.Contains(Path.GetExtension(path));

    public static bool IsAssemblyPath(string path)
        => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
           && !path.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase)
           && !path.Contains("/runtimes/", StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string path)
        => path.Replace('\\', '/').TrimStart('/');

    public static IReadOnlyList<string> ListTypes(byte[] assemblyBytes)
    {
        using var ms = new MemoryStream(assemblyBytes, writable: false);
        using var pe = new PEReader(ms, PEStreamOptions.PrefetchEntireImage);
        if (!pe.HasMetadata)
        {
            return [];
        }

        var md = pe.GetMetadataReader();
        var names = new List<string>();
        foreach (var handle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(handle);
            if (IsNested(type.Attributes))
            {
                continue;
            }

            var name = md.GetString(type.Name);
            if (string.IsNullOrEmpty(name) || name == "<Module>" || name.StartsWith('<'))
            {
                continue;
            }

            var ns = md.GetString(type.Namespace);
            names.Add(string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public static string TypeFilePath(string dllPath, string typeName)
        => $"{Normalize(dllPath)}/{typeName}.cs";

    public static bool TryParseTypePath(string path, out string dllPath, out string typeName)
    {
        dllPath = string.Empty;
        typeName = string.Empty;
        var normalized = Normalize(path);
        var dll = normalized.IndexOf(".dll/", StringComparison.OrdinalIgnoreCase);
        if (dll < 0)
        {
            return false;
        }

        dllPath = normalized[..(dll + 4)];
        var rest = normalized[(dll + 5)..];
        if (!rest.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        typeName = rest[..^3];
        return typeName.Length > 0;
    }

    public static string? ReadZipText(ZipArchive zip, string path)
    {
        var entry = FindEntry(zip, path);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static byte[]? ReadZipBytes(ZipArchive zip, string path)
    {
        var entry = FindEntry(zip, path);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static ZipArchiveEntry? FindEntry(ZipArchive zip, string path)
    {
        var normalized = Normalize(path);
        return zip.Entries.FirstOrDefault(e =>
            string.Equals(Normalize(e.FullName), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<(string? Text, string Kind)> ReadAsync(
        ZipArchive zip,
        string path,
        ISourceLinkContentFetcher sourceLinkFetcher,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(path);
        if (TryParseTypePath(normalized, out var dllPath, out var typeName))
        {
            var dll = ReadZipBytes(zip, dllPath);
            if (dll is null)
            {
                return (null, "Missing");
            }

            var sourceLink = await TrySourceLinkForTypeAsync(zip, dllPath, dll, typeName, sourceLinkFetcher, cancellationToken);
            if (!string.IsNullOrEmpty(sourceLink))
            {
                return (sourceLink, "SourceLink");
            }

            return (DecompileType(dll, dllPath, typeName), "Decompiled");
        }

        if (IsTextPath(normalized))
        {
            return (ReadZipText(zip, normalized), "PackageFile");
        }

        if (IsAssemblyPath(normalized))
        {
            var dll = ReadZipBytes(zip, normalized);
            if (dll is null)
            {
                return (null, "Missing");
            }

            var types = ListTypes(dll);
            var first = types.FirstOrDefault();
            if (first is null)
            {
                return ("// No public types in this assembly.", "Decompiled");
            }

            var sourceLink = await TrySourceLinkForTypeAsync(zip, normalized, dll, first, sourceLinkFetcher, cancellationToken);
            if (!string.IsNullOrEmpty(sourceLink))
            {
                return (sourceLink, "SourceLink");
            }

            return (DecompileType(dll, normalized, first), "Decompiled");
        }

        return (null, "Binary");
    }

    public static string DecompileType(byte[] assemblyBytes, string dllPath, string typeName)
    {
        var temp = Path.Combine(Path.GetTempPath(), "ap-pkg-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var file = Path.Combine(temp, Path.GetFileName(dllPath));
        try
        {
            File.WriteAllBytes(file, assemblyBytes);
            var settings = new DecompilerSettings
            {
                ThrowOnAssemblyResolveErrors = false
            };
            var decompiler = new CSharpDecompiler(file, settings);
            return decompiler.DecompileTypeAsString(new FullTypeName(typeName));
        }
        catch (Exception ex)
        {
            return $"// Decompile failed for {typeName}: {ex.Message}";
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* ignore */ }
        }
    }

    private static async Task<string?> TrySourceLinkForTypeAsync(
        ZipArchive zip,
        string dllPath,
        byte[] assemblyBytes,
        string typeName,
        ISourceLinkContentFetcher sourceLinkFetcher,
        CancellationToken cancellationToken)
    {
        MetadataReaderProvider? provider = null;
        PEReader? pe = null;
        MemoryStream? owned = null;
        try
        {
            if (!TryOpenPdb(zip, dllPath, assemblyBytes, out provider, out pe, out owned, out var pdb))
            {
                return null;
            }

            var simpleName = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
            string? documentPath = null;
            foreach (var docHandle in pdb.Documents)
            {
                var doc = pdb.GetDocument(docHandle);
                var name = pdb.GetString(doc.Name);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(name.Replace('\\', '/'));
                if (string.Equals(fileName, simpleName, StringComparison.OrdinalIgnoreCase)
                    || name.Contains(simpleName + ".cs", StringComparison.OrdinalIgnoreCase))
                {
                    documentPath = name;
                    break;
                }
            }

            if (documentPath is null)
            {
                return null;
            }

            var embedded = TryReadEmbeddedSource(pdb, documentPath);
            if (!string.IsNullOrEmpty(embedded))
            {
                return embedded;
            }

            var map = ReadSourceLinkMap(pdb);
            if (map is null || map.Count == 0)
            {
                return null;
            }

            var url = MapSourceLink(documentPath, map);
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return null;
            }

            return await sourceLinkFetcher.FetchAsync(uri, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
        finally
        {
            provider?.Dispose();
            pe?.Dispose();
            owned?.Dispose();
        }
    }

    private static bool TryOpenPdb(
        ZipArchive zip,
        string dllPath,
        byte[] assemblyBytes,
        out MetadataReaderProvider? provider,
        out PEReader? pe,
        out MemoryStream? owned,
        out MetadataReader pdb)
    {
        provider = null;
        pe = null;
        owned = null;
        pdb = default!;

        var pdbPath = Path.ChangeExtension(dllPath, ".pdb");
        var fromZip = ReadZipBytes(zip, pdbPath);
        if (fromZip is not null)
        {
            owned = new MemoryStream(fromZip, writable: false);
            provider = MetadataReaderProvider.FromPortablePdbStream(owned, MetadataStreamOptions.PrefetchMetadata);
            pdb = provider.GetMetadataReader();
            return true;
        }

        var peStream = new MemoryStream(assemblyBytes, writable: false);
        pe = new PEReader(peStream, PEStreamOptions.PrefetchEntireImage);
        foreach (var entry in pe.ReadDebugDirectory())
        {
            if (entry.Type != DebugDirectoryEntryType.EmbeddedPortablePdb)
            {
                continue;
            }

            provider = pe.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
            pdb = provider.GetMetadataReader();
            owned = peStream;
            return true;
        }

        pe.Dispose();
        pe = null;
        peStream.Dispose();
        return false;
    }

    private static string? TryReadEmbeddedSource(MetadataReader pdb, string documentPath)
    {
        foreach (var docHandle in pdb.Documents)
        {
            var doc = pdb.GetDocument(docHandle);
            var name = pdb.GetString(doc.Name);
            if (!string.Equals(name, documentPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var infoHandle in pdb.GetCustomDebugInformation(docHandle))
            {
                var info = pdb.GetCustomDebugInformation(infoHandle);
                if (pdb.GetGuid(info.Kind) != EmbeddedSourceGuid)
                {
                    continue;
                }

                var bytes = pdb.GetBlobBytes(info.Value);
                if (bytes.Length >= 4 && BitConverter.ToInt32(bytes, 0) == 0)
                {
                    return Encoding.UTF8.GetString(bytes, 4, bytes.Length - 4);
                }
            }
        }

        return null;
    }

    private static Dictionary<string, string>? ReadSourceLinkMap(MetadataReader pdb)
    {
        foreach (var handle in pdb.GetCustomDebugInformation(EntityHandle.ModuleDefinition))
        {
            var info = pdb.GetCustomDebugInformation(handle);
            if (pdb.GetGuid(info.Kind) != SourceLinkGuid)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(pdb.GetBlobBytes(info.Value));
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("documents", out var documents))
            {
                return null;
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in documents.EnumerateObject())
            {
                map[prop.Name] = prop.Value.GetString() ?? string.Empty;
            }

            return map;
        }

        return null;
    }

    public static string? MapSourceLink(string documentPath, IReadOnlyDictionary<string, string> map)
    {
        var path = documentPath.Replace('\\', '/');
        foreach (var (pattern, url) in map)
        {
            var star = pattern.IndexOf('*');
            if (star < 0)
            {
                if (string.Equals(pattern.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }

                continue;
            }

            var prefix = pattern[..star].Replace('\\', '/');
            var suffix = pattern[(star + 1)..].Replace('\\', '/');
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && path.Length >= prefix.Length + suffix.Length)
            {
                var mid = path[prefix.Length..^suffix.Length].Trim('/');
                return url.Replace("*", mid, StringComparison.Ordinal);
            }
        }

        return null;
    }

    private static bool IsNested(TypeAttributes attributes)
        => (attributes & TypeAttributes.VisibilityMask) is TypeAttributes.NestedPublic
            or TypeAttributes.NestedPrivate
            or TypeAttributes.NestedFamily
            or TypeAttributes.NestedAssembly
            or TypeAttributes.NestedFamANDAssem
            or TypeAttributes.NestedFamORAssem;
}
