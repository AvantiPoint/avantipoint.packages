using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using AvantiPoint.Packages.Registry.Native.Storage;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace AvantiPoint.Packages.Registry.Native.Pub;

public sealed partial record PubArchive(string Name, string Version, string PubspecJson)
{
    public static async Task<PubArchive> ReadAsync(Stream archive, NativeRegistryOptions limits, CancellationToken ct)
    {
        using var gzip = new GZipStream(archive, CompressionMode.Decompress, leaveOpen: true);
        using var tar = new TarReader(gzip, leaveOpen: true);
        var names = new HashSet<string>(StringComparer.Ordinal);
        long expanded = 0;
        string? pubspec = null;
        TarEntry? entry;
        while ((entry = await tar.GetNextEntryAsync(copyData: false, cancellationToken: ct)) is not null)
        {
            var name = entry.Name.StartsWith("./", StringComparison.Ordinal) ? entry.Name[2..] : entry.Name;
            if (names.Count >= limits.MaxArchiveEntries || entry.Length > limits.MaxExpandedArchiveBytes - expanded
                || !SafeArchivePath(name) || !names.Add(name))
                throw new InvalidDataException("Invalid pub archive layout or size.");
            expanded += entry.Length;
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.Directory))
                throw new InvalidDataException("Links and special files are not allowed in pub packages.");
            if (name == "pubspec.yaml")
            {
                if (entry.Length > 1024 * 1024 || entry.DataStream is null) throw new InvalidDataException("Invalid pubspec.");
                using var reader = new StreamReader(entry.DataStream, leaveOpen: true);
                pubspec = await reader.ReadToEndAsync(ct);
            }
        }
        if (pubspec is null) throw new InvalidDataException("A root pubspec.yaml is required.");
        // Reject aliases before constructing an object graph and limit nesting.
        var parser = new Parser(new StringReader(pubspec));
        var depth = 0;
        while (parser.MoveNext())
        {
            if (parser.Current is AnchorAlias) throw new InvalidDataException("YAML aliases are not supported.");
            if (parser.Current is MappingStart or SequenceStart && ++depth > 32) throw new InvalidDataException("Pubspec is too deeply nested.");
            if (parser.Current is MappingEnd or SequenceEnd) depth--;
        }
        var deserializer = new DeserializerBuilder().WithDuplicateKeyChecking()
            .WithAttemptingUnquotedStringTypeDeserialization().Build();
        var yaml = deserializer.Deserialize<object>(pubspec);
        var json = JsonSerializer.Serialize(ToJsonValue(yaml, 0));
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("name", out var nameProperty)
            || nameProperty.ValueKind != JsonValueKind.String
            || !document.RootElement.TryGetProperty("version", out var versionProperty)
            || versionProperty.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Pubspec name and version are required strings.");
        var package = nameProperty.GetString()!;
        var version = versionProperty.GetString()!;
        if (!ValidName(package) || !ValidVersion(version)) throw new InvalidDataException("Invalid pub identity.");
        return new(package, version, json);
    }

    public static bool ValidName(string value) => NamePattern().IsMatch(value) && value.Length <= 128;
    public static bool ValidVersion(string value) => value.Length <= 128 && VersionPattern().IsMatch(value);

    private static bool SafeArchivePath(string name) => name.Length is > 0 and <= 1024
        && !name.StartsWith('/') && !name.Contains('\\') && !name.Contains(':') && !name.Any(char.IsControl)
        && name.TrimEnd('/').Split('/').All(p => p.Length > 0 && p is not "." and not "..")
        && !name.Split('/').Any(p => p is ".git" or ".env" or ".netrc" or "credentials.json");

    private static object? ToJsonValue(object? value, int depth)
    {
        if (depth > 32) throw new InvalidDataException("Pubspec is too deeply nested.");
        if (value is IDictionary<object, object> map)
        {
            var result = new Dictionary<string, object?>();
            foreach (var (key, child) in map)
            {
                if (key is not string name || !result.TryAdd(name, ToJsonValue(child, depth + 1)))
                    throw new InvalidDataException("Invalid pubspec mapping key.");
            }
            return result;
        }
        if (value is IEnumerable<object> sequence) return sequence.Select(v => ToJsonValue(v, depth + 1)).ToArray();
        return value;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
