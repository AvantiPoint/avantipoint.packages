using System.Text.Json.Nodes;

namespace AvantiPoint.Packages.UI.Services;

internal static class NpmPackageMetadataReader
{
    public static NpmPackageMetadata Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return NpmPackageMetadata.Empty;
        }

        try
        {
            var meta = JsonNode.Parse(json)?.AsObject();
            if (meta is null)
            {
                return NpmPackageMetadata.Empty;
            }

            var repository = ReadRepository(meta["repository"]);
            var bugs = ReadBugs(meta["bugs"]);
            var dist = meta["dist"] as JsonObject;
            return new NpmPackageMetadata(
                ReadString(meta["description"]),
                ReadKeywords(meta),
                ReadRawString(meta["readme"]),
                ReadString(meta["readmeFilename"]),
                ReadLicense(meta),
                NormalizeWebUrl(ReadString(meta["homepage"])),
                repository.Url,
                repository.Directory,
                bugs.Url,
                bugs.Email,
                ReadPerson(meta["author"]),
                ReadPeople(meta["maintainers"]),
                ReadFunding(meta["funding"]),
                ReadString(meta["deprecated"]),
                ReadString(dist?["integrity"]),
                ReadTarballFileName(ReadString(dist?["tarball"])),
                ReadInt64(dist?["unpackedSize"]),
                ReadInt32(dist?["fileCount"]),
                ReadStringDictionary(meta["dependencies"]),
                ReadStringDictionary(meta["devDependencies"]),
                ReadStringDictionary(meta["peerDependencies"]),
                ReadStringDictionary(meta["optionalDependencies"]),
                ReadOptionalPeerDependencies(meta["peerDependenciesMeta"]),
                ReadStringArray(meta["bundledDependencies"] ?? meta["bundleDependencies"]),
                ReadStringDictionary(meta["engines"]),
                ReadString(meta["packageManager"]),
                ReadString(meta["type"]),
                ReadString(meta["types"] ?? meta["typings"]),
                ReadStringArray(meta["os"]),
                ReadStringArray(meta["cpu"]),
                ReadPerson(meta["_npmUser"]),
                ReadString(meta["_npmVersion"]),
                ReadString(meta["_nodeVersion"]),
                ReadString(meta["gitHead"]),
                ReadRegistrySignatureCount(dist),
                ReadProvenance(dist));
        }
        catch (Exception)
        {
            // A malformed third-party packument must not take down the gallery.
            return NpmPackageMetadata.Empty;
        }
    }

    private static IReadOnlyList<string>? ReadKeywords(JsonObject meta)
    {
        if (!meta.ContainsKey("keywords"))
        {
            return null;
        }

        if (meta["keywords"] is JsonArray array)
        {
            return array
                .Select(ReadString)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToList();
        }

        if (meta["keywords"] is JsonValue value
            && value.TryGetValue<string>(out var joined)
            && !string.IsNullOrWhiteSpace(joined))
        {
            return joined.Split(
                [',', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [];
    }

    private static string? ReadLicense(JsonObject meta)
    {
        if (ReadString(meta["license"]) is { } license)
        {
            return license;
        }

        if (meta["license"] is JsonObject licenseObject
            && ReadString(licenseObject["type"]) is { } type)
        {
            return type;
        }

        if (meta["licenses"] is JsonArray licenses)
        {
            var values = licenses
                .Select(item => item is JsonObject itemObject
                    ? ReadString(itemObject["type"])
                    : ReadString(item))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return values.Count == 0 ? null : string.Join(" OR ", values);
        }

        return null;
    }

    private static (string? Url, string? Directory) ReadRepository(JsonNode? node)
    {
        if (ReadString(node) is { } repository)
        {
            return (NormalizeRepositoryUrl(repository), null);
        }

        if (node is not JsonObject repositoryObject)
        {
            return (null, null);
        }

        return (
            NormalizeRepositoryUrl(ReadString(repositoryObject["url"])),
            ReadString(repositoryObject["directory"]));
    }

    private static (string? Url, string? Email) ReadBugs(JsonNode? node)
    {
        if (ReadString(node) is { } bugsUrl)
        {
            return (NormalizeWebUrl(bugsUrl), null);
        }

        if (node is not JsonObject bugsObject)
        {
            return (null, null);
        }

        return (
            NormalizeWebUrl(ReadString(bugsObject["url"])),
            ReadString(bugsObject["email"]));
    }

    private static NpmPersonModel? ReadPerson(JsonNode? node)
    {
        if (ReadString(node) is { } displayName)
        {
            return new NpmPersonModel(displayName);
        }

        if (node is not JsonObject person)
        {
            return null;
        }

        var name = ReadString(person["name"]);
        var email = ReadString(person["email"]);
        var url = NormalizeWebUrl(ReadString(person["url"]));
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new NpmPersonModel(name ?? email!, email, url);
    }

    private static IReadOnlyList<NpmPersonModel>? ReadPeople(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is not JsonArray people)
        {
            return [];
        }

        return people
            .Select(ReadPerson)
            .Where(person => person is not null)
            .Cast<NpmPersonModel>()
            .ToList();
    }

    private static IReadOnlyList<NpmFundingLinkModel>? ReadFunding(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        var links = new List<NpmFundingLinkModel>();
        AddFunding(node, links);
        return links;
    }

    private static int ReadRegistrySignatureCount(JsonObject? dist)
    {
        if (dist?["signatures"] is JsonArray signatures)
        {
            var count = signatures.Count(signature =>
                signature is JsonObject value
                && !string.IsNullOrWhiteSpace(ReadString(value["sig"])));
            if (count > 0)
            {
                return count;
            }
        }

        return string.IsNullOrWhiteSpace(ReadString(dist?["npm-signature"])) ? 0 : 1;
    }

    private static NpmProvenanceModel? ReadProvenance(JsonObject? dist)
    {
        if (dist?["attestations"] is not JsonObject attestations)
        {
            return null;
        }

        var url = NormalizeWebUrl(ReadString(attestations["url"]));
        var predicateType = attestations["provenance"] is JsonObject provenance
            ? ReadString(provenance["predicateType"])
            : null;
        return url is null && predicateType is null
            ? null
            : new NpmProvenanceModel(url, predicateType);
    }

    private static void AddFunding(JsonNode? node, ICollection<NpmFundingLinkModel> links)
    {
        if (ReadString(node) is { } rawUrl)
        {
            if (NormalizeWebUrl(rawUrl) is { } url)
            {
                links.Add(new NpmFundingLinkModel(url));
            }

            return;
        }

        if (node is JsonObject funding)
        {
            if (NormalizeWebUrl(ReadString(funding["url"])) is { } url)
            {
                links.Add(new NpmFundingLinkModel(url, ReadString(funding["type"])));
            }

            return;
        }

        if (node is JsonArray fundingItems)
        {
            foreach (var item in fundingItems)
            {
                AddFunding(item, links);
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ReadStringDictionary(JsonNode? node)
    {
        if (node is not JsonObject values)
        {
            return new Dictionary<string, string>();
        }

        var result = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in values)
        {
            if (ReadString(entry.Value) is { } value)
            {
                result[entry.Key] = value;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ReadOptionalPeerDependencies(JsonNode? node)
    {
        if (node is not JsonObject metadata)
        {
            return [];
        }

        return metadata
            .Where(entry => entry.Value is JsonObject details
                && details["optional"] is JsonValue optional
                && optional.TryGetValue<bool>(out var isOptional)
                && isOptional)
            .Select(entry => entry.Key)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
    {
        if (node is not JsonArray values)
        {
            return [];
        }

        return values
            .Select(ReadString)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();
    }

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value
        && value.TryGetValue<string>(out var text)
        && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    private static string? ReadRawString(JsonNode? node) =>
        node is JsonValue value
        && value.TryGetValue<string>(out var text)
        && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static long? ReadInt64(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<long>(out var longValue))
        {
            return longValue;
        }

        return value.TryGetValue<int>(out var intValue) ? intValue : null;
    }

    private static int? ReadInt32(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        return value.TryGetValue<long>(out var longValue) && longValue is >= int.MinValue and <= int.MaxValue
            ? (int)longValue
            : null;
    }

    private static string? ReadTarballFileName(string? tarball)
    {
        if (string.IsNullOrWhiteSpace(tarball))
        {
            return null;
        }

        return Uri.TryCreate(tarball, UriKind.Absolute, out var uri)
            ? Path.GetFileName(uri.AbsolutePath)
            : Path.GetFileName(tarball);
    }

    private static string? NormalizeRepositoryUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var candidate = url.Trim();
        if (candidate.StartsWith("git+", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[4..];
        }
        else if (candidate.StartsWith("git://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = $"https://{candidate[6..]}";
        }

        return NormalizeWebUrl(candidate);
    }

    private static string? NormalizeWebUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.ToString();
    }
}

internal sealed record NpmPackageMetadata(
    string? Description,
    IReadOnlyList<string>? Keywords,
    string? Readme,
    string? ReadmeFileName,
    string? License,
    string? HomepageUrl,
    string? RepositoryUrl,
    string? RepositoryDirectory,
    string? BugsUrl,
    string? BugsEmail,
    NpmPersonModel? Author,
    IReadOnlyList<NpmPersonModel>? Maintainers,
    IReadOnlyList<NpmFundingLinkModel>? Funding,
    string? Deprecated,
    string? Integrity,
    string? TarballFileName,
    long? UnpackedSize,
    int? FileCount,
    IReadOnlyDictionary<string, string> Dependencies,
    IReadOnlyDictionary<string, string> DevDependencies,
    IReadOnlyDictionary<string, string> PeerDependencies,
    IReadOnlyDictionary<string, string> OptionalDependencies,
    IReadOnlyList<string> OptionalPeerDependencies,
    IReadOnlyList<string> BundledDependencies,
    IReadOnlyDictionary<string, string> Engines,
    string? PackageManager,
    string? ModuleType,
    string? TypesEntryPoint,
    IReadOnlyList<string> OperatingSystems,
    IReadOnlyList<string> CpuArchitectures,
    NpmPersonModel? Publisher,
    string? PublishingNpmVersion,
    string? PublishingNodeVersion,
    string? GitHead,
    int RegistrySignatureCount,
    NpmProvenanceModel? Provenance)
{
    public static NpmPackageMetadata Empty { get; } = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        [],
        [],
        new Dictionary<string, string>(),
        null,
        null,
        null,
        [],
        [],
        null,
        null,
        null,
        null,
        0,
        null);
}
