using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.UI.Services;

public sealed record NpmPackageDetailModel(
    string Name,
    IReadOnlyList<NpmVersionListItem> Versions,
    IReadOnlyDictionary<string, string> DistTags,
    string? Description = null,
    IReadOnlyList<string>? Keywords = null,
    string? Readme = null,
    string? ReadmeFileName = null,
    string? License = null,
    string? HomepageUrl = null,
    string? RepositoryUrl = null,
    string? RepositoryDirectory = null,
    string? BugsUrl = null,
    string? BugsEmail = null,
    NpmPersonModel? Author = null,
    IReadOnlyList<NpmPersonModel>? Maintainers = null,
    IReadOnlyList<NpmFundingLinkModel>? Funding = null)
{
    /// <summary>
    /// Package-level metadata is the npm packument fallback for version objects that omit a field.
    /// Version metadata takes precedence whenever that field is present.
    /// </summary>
    public NpmPackagePresentationMetadata FallbackMetadata => new(
        Description,
        Keywords,
        Readme,
        ReadmeFileName,
        License,
        HomepageUrl,
        RepositoryUrl,
        RepositoryDirectory,
        BugsUrl,
        BugsEmail,
        Author,
        Maintainers,
        Funding);
}

public sealed record NpmVersionListItem(
    string Version,
    DateTime Published,
    string? Shasum,
    PackageOrigin Origin = PackageOrigin.Published,
    string? Deprecated = null,
    string? Integrity = null,
    string? TarballFileName = null,
    long? UnpackedSize = null,
    int? FileCount = null,
    IReadOnlyDictionary<string, string>? Dependencies = null,
    IReadOnlyDictionary<string, string>? DevDependencies = null,
    IReadOnlyDictionary<string, string>? PeerDependencies = null,
    IReadOnlyDictionary<string, string>? OptionalDependencies = null,
    IReadOnlyList<string>? OptionalPeerDependencies = null,
    IReadOnlyList<string>? BundledDependencies = null,
    IReadOnlyDictionary<string, string>? Engines = null,
    string? PackageManager = null,
    string? ModuleType = null,
    string? TypesEntryPoint = null,
    IReadOnlyList<string>? OperatingSystems = null,
    IReadOnlyList<string>? CpuArchitectures = null,
    NpmPackagePresentationMetadata? Metadata = null,
    NpmPersonModel? Publisher = null,
    string? PublishingNpmVersion = null,
    string? PublishingNodeVersion = null,
    string? GitHead = null,
    int RegistrySignatureCount = 0,
    NpmProvenanceModel? Provenance = null);

public sealed record NpmPackagePresentationMetadata(
    string? Description = null,
    IReadOnlyList<string>? Keywords = null,
    string? Readme = null,
    string? ReadmeFileName = null,
    string? License = null,
    string? HomepageUrl = null,
    string? RepositoryUrl = null,
    string? RepositoryDirectory = null,
    string? BugsUrl = null,
    string? BugsEmail = null,
    NpmPersonModel? Author = null,
    IReadOnlyList<NpmPersonModel>? Maintainers = null,
    IReadOnlyList<NpmFundingLinkModel>? Funding = null)
{
    public static NpmPackagePresentationMetadata Empty { get; } = new();

    public NpmPackagePresentationMetadata WithFallback(NpmPackagePresentationMetadata fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return new(
            Description ?? fallback.Description,
            Keywords ?? fallback.Keywords,
            Readme ?? fallback.Readme,
            ReadmeFileName ?? fallback.ReadmeFileName,
            License ?? fallback.License,
            HomepageUrl ?? fallback.HomepageUrl,
            RepositoryUrl ?? fallback.RepositoryUrl,
            RepositoryDirectory ?? fallback.RepositoryDirectory,
            BugsUrl ?? fallback.BugsUrl,
            BugsEmail ?? fallback.BugsEmail,
            Author ?? fallback.Author,
            Maintainers ?? fallback.Maintainers,
            Funding ?? fallback.Funding);
    }
}

public sealed record NpmPersonModel(
    string Name,
    string? Email = null,
    string? Url = null);

public sealed record NpmFundingLinkModel(
    string Url,
    string? Type = null);

public sealed record NpmProvenanceModel(
    string? Url,
    string? PredicateType);
