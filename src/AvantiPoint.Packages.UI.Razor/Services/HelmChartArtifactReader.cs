using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AvantiPoint.Packages.Registry.Oci;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Reads human-facing metadata and documents from a Helm chart stored as an OCI artifact.
/// No archive content is written to disk and every untrusted input is bounded.
/// </summary>
public static partial class HelmChartArtifactReader
{
    public const long MaximumConfigBytes = 1024 * 1024;
    public const long MaximumCompressedChartBytes = 32 * 1024 * 1024;
    public const long MaximumExpandedChartBytes = 128 * 1024 * 1024;
    public const int MaximumDocumentBytes = 2 * 1024 * 1024;
    public const int MaximumArchiveEntries = 4096;

    private const string HelmConfigMediaType = "application/vnd.cncf.helm.config.v1+json";
    private const string HelmChartContentMediaType = "application/vnd.cncf.helm.chart.content.v1.tar+gzip";
    private const string HelmProvenanceMediaType = "application/vnd.cncf.helm.chart.provenance.v1.prov";
    private const string LegacyChartContentMediaType = "application/tar+gzip";
    private const int MaximumCandidateDocuments = 32;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
    };

    public static async Task<HelmChartDetails> ReadAsync(
        ReadOnlyMemory<byte> manifestContent,
        Func<string, CancellationToken, Task<OciBlobResult?>> loadBlob,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loadBlob);

        HelmManifestDescriptors descriptors;
        try
        {
            descriptors = ReadDescriptors(manifestContent);
        }
        catch (JsonException)
        {
            return EmptyDetails("Chart content is unavailable because the OCI manifest is invalid.");
        }

        var errors = new List<string>();
        HelmChartMetadataDocument? metadata = null;
        if (descriptors.Config is null)
        {
            errors.Add("Chart metadata is not available in this artifact.");
        }
        else
        {
            metadata = await ReadConfigMetadataAsync(descriptors.Config, loadBlob, cancellationToken);
            if (metadata is null)
            {
                errors.Add("Chart metadata could not be read from the stored OCI config.");
            }
        }

        HelmChartDocuments documents;
        if (descriptors.ChartLayerCount == 0)
        {
            documents = HelmChartDocuments.Empty;
            errors.Add("The chart archive is not available in this artifact.");
        }
        else if (descriptors.ChartLayerCount > 1 || descriptors.ChartLayer is null)
        {
            documents = HelmChartDocuments.Empty;
            errors.Add("The artifact contains an ambiguous chart archive and was not inspected.");
        }
        else
        {
            var documentResult = await ReadChartDocumentsAsync(
                descriptors.ChartLayer,
                loadBlob,
                cancellationToken);
            documents = documentResult.Documents;
            if (documentResult.Error is not null)
            {
                errors.Add(documentResult.Error);
            }
        }

        var readmeHtml = string.IsNullOrWhiteSpace(documents.Readme)
            ? null
            : NpmReadmeRenderer.Render(documents.Readme);

        return new HelmChartDetails(
            metadata?.Name,
            metadata?.Version,
            metadata?.AppVersion,
            metadata?.Description,
            metadata?.ApiVersion,
            metadata?.KubeVersion,
            metadata?.Type,
            metadata?.Home,
            metadata?.Icon,
            metadata?.Deprecated ?? false,
            CleanStrings(metadata?.Sources),
            CleanStrings(metadata?.Keywords),
            ToMaintainers(metadata?.Maintainers),
            ToDependencies(metadata?.Dependencies),
            readmeHtml,
            documents.ValuesYaml,
            PrettyPrintJson(documents.ValuesSchemaJson),
            documents.ChartYaml,
            documents.ChartLockYaml,
            descriptors.HasProvenance,
            errors.Count == 0 ? null : string.Join(' ', errors));
    }

    private static HelmManifestDescriptors ReadDescriptors(ReadOnlyMemory<byte> manifestContent)
    {
        using var document = JsonDocument.Parse(manifestContent.ToArray());
        var root = document.RootElement;
        var config = root.TryGetProperty("config", out var configElement)
            ? ReadDescriptor(configElement, MaximumConfigBytes)
            : null;
        if (config is not null
            && !string.Equals(config.MediaType, HelmConfigMediaType, StringComparison.OrdinalIgnoreCase))
        {
            config = null;
        }

        HelmBlobDescriptor? chartLayer = null;
        var chartLayerCount = 0;
        var hasProvenance = false;
        if (root.TryGetProperty("layers", out var layers)
            && layers.ValueKind == JsonValueKind.Array)
        {
            foreach (var layer in layers.EnumerateArray())
            {
                var descriptor = ReadDescriptor(layer, MaximumCompressedChartBytes);
                if (descriptor is null)
                {
                    continue;
                }

                if (string.Equals(
                        descriptor.MediaType,
                        HelmChartContentMediaType,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        descriptor.MediaType,
                        LegacyChartContentMediaType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    chartLayerCount++;
                    chartLayer ??= descriptor;
                }
                else if (string.Equals(
                             descriptor.MediaType,
                             HelmProvenanceMediaType,
                             StringComparison.OrdinalIgnoreCase))
                {
                    hasProvenance = true;
                }
            }
        }

        return new HelmManifestDescriptors(config, chartLayer, chartLayerCount, hasProvenance);
    }

    private static HelmBlobDescriptor? ReadDescriptor(JsonElement element, long maximumSize)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("mediaType", out var mediaTypeElement)
            || !element.TryGetProperty("digest", out var digestElement)
            || !element.TryGetProperty("size", out var sizeElement)
            || !sizeElement.TryGetInt64(out var size)
            || size < 0
            || size > maximumSize)
        {
            return null;
        }

        var mediaType = NormalizeMediaType(mediaTypeElement.GetString());
        var digest = digestElement.GetString();
        return string.IsNullOrEmpty(mediaType) || !IsValidDigest(digest)
            ? null
            : new HelmBlobDescriptor(digest!, mediaType, size);
    }

    private static async Task<HelmChartMetadataDocument?> ReadConfigMetadataAsync(
        HelmBlobDescriptor descriptor,
        Func<string, CancellationToken, Task<OciBlobResult?>> loadBlob,
        CancellationToken cancellationToken)
    {
        var blob = await loadBlob(descriptor.Digest, cancellationToken);
        if (blob is null)
        {
            return null;
        }

        await using var content = blob.Content;
        if (blob.Size != descriptor.Size || blob.Size is < 0 or > MaximumConfigBytes)
        {
            return null;
        }

        try
        {
            var bytes = await ReadBoundedAsync(content, MaximumConfigBytes, cancellationToken);
            return bytes is null || bytes.LongLength != descriptor.Size || !DigestMatches(descriptor.Digest, bytes)
                ? null
                : JsonSerializer.Deserialize<HelmChartMetadataDocument>(bytes, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<HelmDocumentReadResult> ReadChartDocumentsAsync(
        HelmBlobDescriptor descriptor,
        Func<string, CancellationToken, Task<OciBlobResult?>> loadBlob,
        CancellationToken cancellationToken)
    {
        var blob = await loadBlob(descriptor.Digest, cancellationToken);
        if (blob is null)
        {
            return HelmDocumentReadResult.Unavailable("The chart archive blob is not available.");
        }

        await using var content = blob.Content;
        if (blob.Size != descriptor.Size || blob.Size is < 0 or > MaximumCompressedChartBytes)
        {
            return HelmDocumentReadResult.Unavailable("The chart archive exceeds the inspection limit.");
        }

        try
        {
            var archiveBytes = await ReadBoundedAsync(
                content,
                MaximumCompressedChartBytes,
                cancellationToken);
            if (archiveBytes is null
                || archiveBytes.LongLength != descriptor.Size
                || !DigestMatches(descriptor.Digest, archiveBytes))
            {
                return HelmDocumentReadResult.Unavailable(
                    "The chart archive failed its OCI digest or size check.");
            }

            using var archive = new MemoryStream(archiveBytes, writable: false);
            using var gzip = new GZipStream(archive, CompressionMode.Decompress, leaveOpen: false);
            using var expanded = new SizeLimitedReadStream(gzip, MaximumExpandedChartBytes);
            using var tar = new TarReader(expanded, leaveOpen: true);
            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var entryIndex = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = tar.GetNextEntry(copyData: false);
                if (entry is null)
                {
                    break;
                }

                entryIndex++;
                if (entryIndex > MaximumArchiveEntries)
                {
                    return HelmDocumentReadResult.Unavailable(
                        "The chart archive contains too many files to inspect safely.");
                }

                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    continue;
                }

                var path = NormalizeCandidatePath(entry.Name);
                if (path is null || entry.DataStream is null || entry.Length < 0)
                {
                    continue;
                }

                if (entry.Length > MaximumDocumentBytes)
                {
                    return HelmDocumentReadResult.Unavailable(
                        $"The chart document '{GetFileName(path)}' exceeds the inspection limit.");
                }

                if (candidates.Count >= MaximumCandidateDocuments)
                {
                    return HelmDocumentReadResult.Unavailable(
                        "The chart archive contains an ambiguous set of root documents.");
                }

                var bytes = await ReadBoundedAsync(
                    entry.DataStream,
                    MaximumDocumentBytes,
                    cancellationToken);
                if (bytes is null)
                {
                    return HelmDocumentReadResult.Unavailable(
                        $"The chart document '{GetFileName(path)}' exceeds the inspection limit.");
                }

                try
                {
                    if (!candidates.TryAdd(path, StrictUtf8.GetString(bytes).TrimStart('\uFEFF')))
                    {
                        return HelmDocumentReadResult.Unavailable(
                            $"The chart archive contains a duplicate root document '{GetFileName(path)}' and was not inspected.");
                    }
                }
                catch (DecoderFallbackException)
                {
                    return HelmDocumentReadResult.Unavailable(
                        $"The chart document '{GetFileName(path)}' is not valid UTF-8.");
                }
            }

            return SelectChartDocuments(candidates);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            return HelmDocumentReadResult.Unavailable(
                "The chart archive is invalid or exceeds the inspection limit.");
        }
    }

    private static HelmDocumentReadResult SelectChartDocuments(
        IReadOnlyDictionary<string, string> candidates)
    {
        var chartPaths = candidates.Keys
            .Where(path => GetFileName(path).Equals("Chart.yaml", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (chartPaths.Length == 0)
        {
            return HelmDocumentReadResult.Unavailable(
                "The chart archive does not contain a root Chart.yaml file.");
        }

        var roots = chartPaths
            .Select(GetDirectoryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roots.Length != 1 || chartPaths.Length != 1)
        {
            return HelmDocumentReadResult.Unavailable(
                "The chart archive contains multiple chart roots and was not inspected.");
        }

        var root = roots[0];
        var chartPath = chartPaths[0];
        return new HelmDocumentReadResult(
            new HelmChartDocuments(
                candidates[chartPath],
                FindSibling(candidates, root, "README.md", "README", "README.txt"),
                FindSibling(candidates, root, "values.yaml", "values.yml"),
                FindSibling(candidates, root, "values.schema.json"),
                FindSibling(candidates, root, "Chart.lock")),
            null);
    }

    private static string? NormalizeCandidatePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.IndexOf('\0') >= 0
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith('\\'))
        {
            return null;
        }

        var normalized = value.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        var segments = normalized.Split('/');
        if (segments.Any(static segment => string.IsNullOrEmpty(segment) || segment is "." or "..")
            || segments.Length is < 1 or > 2
            || !IsInterestingFileName(segments[^1]))
        {
            return null;
        }

        return string.Join('/', segments);
    }

    private static bool IsInterestingFileName(string fileName) =>
        fileName.Equals("Chart.yaml", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("Chart.lock", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("README", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("README.txt", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("values.yaml", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("values.yml", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("values.schema.json", StringComparison.OrdinalIgnoreCase);

    private static string? FindSibling(
        IReadOnlyDictionary<string, string> candidates,
        string root,
        params string[] names)
    {
        foreach (var name in names)
        {
            var path = string.IsNullOrEmpty(root) ? name : $"{root}/{name}";
            if (candidates.TryGetValue(path, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> CleanStrings(IEnumerable<string?>? values) =>
        values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];

    private static IReadOnlyList<HelmChartMaintainer> ToMaintainers(
        IEnumerable<HelmMaintainerDocument>? maintainers) =>
        maintainers?
            .Where(static maintainer => !string.IsNullOrWhiteSpace(maintainer.Name))
            .Select(static maintainer => new HelmChartMaintainer(
                maintainer.Name!.Trim(),
                NullIfWhiteSpace(maintainer.Email),
                NullIfWhiteSpace(maintainer.Url)))
            .ToArray()
        ?? [];

    private static IReadOnlyList<HelmChartDependency> ToDependencies(
        IEnumerable<HelmDependencyDocument>? dependencies) =>
        dependencies?
            .Where(static dependency => !string.IsNullOrWhiteSpace(dependency.Name))
            .Select(static dependency => new HelmChartDependency(
                dependency.Name!.Trim(),
                NullIfWhiteSpace(dependency.Version),
                NullIfWhiteSpace(dependency.Repository),
                NullIfWhiteSpace(dependency.Condition),
                NullIfWhiteSpace(dependency.Alias),
                CleanStrings(dependency.Tags)))
            .ToArray()
        ?? [];

    private static string? PrettyPrintJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement, IndentedJsonOptions);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream((int)Math.Min(maximumBytes, 64 * 1024));
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maximumBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }

    private static HelmChartDetails EmptyDetails(string unavailableReason) => new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false,
        [],
        [],
        [],
        [],
        null,
        null,
        null,
        null,
        null,
        false,
        unavailableReason);

    private static bool IsValidDigest(string? value) =>
        !string.IsNullOrWhiteSpace(value) && DigestPattern().IsMatch(value);

    private static bool DigestMatches(string digest, byte[] content)
    {
        var expected = digest["sha256:".Length..];
        var actual = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMediaType(string? value)
    {
        var mediaType = value?.Trim() ?? string.Empty;
        var parameter = mediaType.IndexOf(';');
        return parameter < 0 ? mediaType : mediaType[..parameter].Trim();
    }

    private static string GetFileName(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? path : path[(separator + 1)..];
    }

    private static string GetDirectoryName(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..separator];
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^sha256:[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();

    private sealed record HelmManifestDescriptors(
        HelmBlobDescriptor? Config,
        HelmBlobDescriptor? ChartLayer,
        int ChartLayerCount,
        bool HasProvenance);

    private sealed record HelmBlobDescriptor(string Digest, string MediaType, long Size);

    private sealed record HelmDocumentReadResult(HelmChartDocuments Documents, string? Error)
    {
        public static HelmDocumentReadResult Unavailable(string error) =>
            new(HelmChartDocuments.Empty, error);
    }

    private sealed record HelmChartDocuments(
        string? ChartYaml,
        string? Readme,
        string? ValuesYaml,
        string? ValuesSchemaJson,
        string? ChartLockYaml)
    {
        public static HelmChartDocuments Empty { get; } = new(null, null, null, null, null);
    }

    private sealed class HelmChartMetadataDocument
    {
        public string? ApiVersion { get; set; }

        public string? Name { get; set; }

        public string? Version { get; set; }

        public string? AppVersion { get; set; }

        public string? KubeVersion { get; set; }

        public string? Description { get; set; }

        public string? Type { get; set; }

        public string? Home { get; set; }

        public string? Icon { get; set; }

        public bool? Deprecated { get; set; }

        public List<string>? Sources { get; set; }

        public List<string>? Keywords { get; set; }

        public List<HelmMaintainerDocument>? Maintainers { get; set; }

        public List<HelmDependencyDocument>? Dependencies { get; set; }
    }

    private sealed class HelmMaintainerDocument
    {
        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? Url { get; set; }
    }

    private sealed class HelmDependencyDocument
    {
        public string? Name { get; set; }

        public string? Version { get; set; }

        public string? Repository { get; set; }

        public string? Condition { get; set; }

        public string? Alias { get; set; }

        public List<string>? Tags { get; set; }
    }

    private sealed class SizeLimitedReadStream(Stream inner, long maximumBytes) : Stream
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

        private int Count(int read)
        {
            _bytesRead += read;
            if (_bytesRead > maximumBytes)
            {
                throw new InvalidDataException("Stream exceeds the Helm chart inspection limit.");
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
