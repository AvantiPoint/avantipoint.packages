using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;

namespace AvantiPoint.Packages.UI.Services;

public sealed record OciRepositoryListItem(
    string Name,
    OciArtifactKind ArtifactKind = OciArtifactKind.Unknown,
    string? LatestTag = null,
    string? LatestDigest = null,
    string? PlatformOs = null,
    string? PlatformArch = null,
    int TagCount = 0,
    long? Size = null,
    DateTime? UpdatedAt = null,
    PackageOrigin Origin = PackageOrigin.Published);

public sealed record OciRepositorySearchResult(
    IReadOnlyList<OciRepositoryListItem> Items,
    int TotalCount,
    int Skip,
    int Take)
{
    public bool HasPreviousPage => Skip > 0;

    public bool HasNextPage => Skip + Items.Count < TotalCount;
}

public sealed record OciTagListItem(
    string Tag,
    string Digest,
    OciArtifactKind ArtifactKind,
    long? Size,
    DateTime? UpdatedAt,
    string? PlatformOs,
    string? PlatformArch,
    PackageOrigin Origin);

public sealed record OciRepositoryTagsModel(
    string RepositoryName,
    OciArtifactKind ArtifactKind,
    IReadOnlyList<OciTagListItem> Tags);

public sealed record OciArtifactDetailModel(
    string RepositoryName,
    string Reference,
    string Digest,
    string MediaType,
    OciArtifactKind ArtifactKind,
    string? PlatformOs,
    string? PlatformArch,
    long Size,
    IReadOnlyList<string> Tags,
    string RegistryRootUrl,
    DateTime? CreatedAt,
    PackageOrigin Origin,
    int ReferencedArtifactCount,
    OciDockerImageDetails? DockerDetails = null,
    HelmChartDetails? HelmDetails = null,
    HelmChartVersionComparison? HelmComparison = null,
    OciDockerImageComparison? DockerComparison = null);

public sealed record HelmChartDetails(
    string? Name,
    string? Version,
    string? AppVersion,
    string? Description,
    string? ApiVersion,
    string? KubeVersion,
    string? Type,
    string? Home,
    string? Icon,
    bool Deprecated,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<HelmChartMaintainer> Maintainers,
    IReadOnlyList<HelmChartDependency> Dependencies,
    string? ReadmeHtml,
    string? ValuesYaml,
    string? ValuesSchemaJson,
    string? ChartYaml,
    string? ChartLockYaml,
    bool HasProvenance,
    string? UnavailableReason);

public sealed record HelmChartMaintainer(
    string Name,
    string? Email,
    string? Url);

public sealed record HelmChartDependency(
    string Name,
    string? Version,
    string? Repository,
    string? Condition,
    string? Alias,
    IReadOnlyList<string> Tags);

public sealed record HelmChartVersionComparison(
    string PreviousVersion,
    string CurrentVersion,
    IReadOnlyList<HelmChartDocumentComparison> Documents)
{
    public bool HasChanges => Documents.Any(static document => document.ChangeKind != "unchanged");

    public int ChangedDocumentCount => Documents.Count(static document => document.ChangeKind != "unchanged");

    public int AddedLineCount => Documents.Sum(static document => document.AddedLineCount ?? 0);

    public int RemovedLineCount => Documents.Sum(static document => document.RemovedLineCount ?? 0);
}

public sealed record HelmChartDocumentComparison(
    string Label,
    string FileName,
    string ChangeKind,
    int? AddedLineCount,
    int? RemovedLineCount,
    IReadOnlyList<DiffLine> Lines,
    bool PreviewUnavailable);

public sealed record OciDockerImageDetails(
    DateTimeOffset? ImageCreatedAt,
    string? OperatingSystem,
    string? Architecture,
    string? Variant,
    IReadOnlyList<string> Entrypoint,
    IReadOnlyList<string> Command,
    string? WorkingDirectory,
    string? User,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<OciDockerLayer> Layers,
    IReadOnlyList<OciDockerHistoryEntry> History,
    IReadOnlyList<OciDockerPlatform> Platforms,
    IReadOnlyDictionary<string, string>? ManifestAnnotations = null,
    IReadOnlyList<OciDockerSupplyChainArtifact>? SupplyChainArtifacts = null,
    string? ManifestDigest = null,
    OciVulnerabilityPolicyAssessment? VulnerabilityPolicyAssessment = null);

public sealed record OciDockerSupplyChainArtifact(
    OciDockerSupplyChainArtifactKind Kind,
    string Reference,
    string ManifestDigest,
    string? ArtifactType = null,
    bool IsOciReferrer = false,
    OciVulnerabilityReportSummary? VulnerabilitySummary = null);

public sealed record OciVulnerabilityReportSummary(
    string Format,
    int Critical,
    int High,
    int Medium,
    int Low,
    int Negligible,
    int Unknown)
{
    public int Total => Critical + High + Medium + Low + Negligible + Unknown;
}

public sealed record OciVulnerabilityPolicyAssessment(
    OciVulnerabilityPolicyStatus Status,
    string Message,
    IReadOnlyList<string> Violations);

public enum OciVulnerabilityPolicyStatus
{
    NotConfigured,
    Passed,
    Failed,
    NotEvaluated,
}

public enum OciDockerSupplyChainArtifactKind
{
    Signature,
    Attestation,
    Sbom,
    VulnerabilityReport,
    Other,
}

public sealed record OciDockerLayer(
    int Number,
    string Digest,
    string? MediaType,
    long? Size);

public sealed record OciDockerHistoryEntry(
    DateTimeOffset? CreatedAt,
    string? CreatedBy,
    string? Comment,
    bool EmptyLayer);

public sealed record OciDockerPlatform(
    string Digest,
    string? OperatingSystem,
    string? Architecture,
    string? Variant,
    long? Size);

public sealed record OciDockerImageComparison(
    string PreviousVersion,
    string CurrentVersion,
    IReadOnlyList<OciDockerValueChange> RuntimeChanges,
    IReadOnlyList<OciDockerKeyValueChange> LabelChanges,
    IReadOnlyList<OciDockerKeyValueChange> AnnotationChanges,
    IReadOnlyList<OciDockerPlatformChange> PlatformChanges,
    int SharedBaseLayerCount,
    int ReusedLayerCount,
    IReadOnlyList<OciDockerLayer> AddedLayers,
    IReadOnlyList<OciDockerLayer> RemovedLayers,
    bool LayerOrderChanged)
{
    public bool HasChanges =>
        RuntimeChanges.Count > 0
        || LabelChanges.Count > 0
        || AnnotationChanges.Count > 0
        || PlatformChanges.Count > 0
        || AddedLayers.Count > 0
        || RemovedLayers.Count > 0
        || LayerOrderChanged;

    public long AddedLayerBytes => AddedLayers.Sum(static layer => layer.Size ?? 0);
}

public sealed record OciDockerValueChange(
    string Label,
    string? PreviousValue,
    string? CurrentValue);

public sealed record OciDockerKeyValueChange(
    string Key,
    string ChangeKind,
    string? PreviousValue,
    string? CurrentValue);

public sealed record OciDockerPlatformChange(
    string Platform,
    string ChangeKind,
    string? PreviousDigest,
    string? CurrentDigest);

public static class OciCatalogKind
{
    public const string Docker = "Docker";
    public const string Helm = "Helm";
    public const string Unknown = "Unknown";

    public static string ToDisplay(OciArtifactKind kind) => kind switch
    {
        OciArtifactKind.Image or OciArtifactKind.Index => Docker,
        OciArtifactKind.Helm => Helm,
        _ => Unknown,
    };

    public static bool MatchesFilter(OciArtifactKind kind, string? filter)
    {
        if (string.IsNullOrEmpty(filter) || filter == "All")
        {
            return true;
        }

        return string.Equals(ToDisplay(kind), filter, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<OciArtifactKind> GetArtifactKinds(string? filter) => filter switch
    {
        Docker => [OciArtifactKind.Image, OciArtifactKind.Index],
        Helm => [OciArtifactKind.Helm],
        _ => [],
    };
}
