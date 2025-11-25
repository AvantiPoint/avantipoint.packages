#nullable enable

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Protocol capability information discovered from an upstream service index.
/// </summary>
public class PackageSourceMetadataProtocol
{
    /// <summary>
    /// Version string reported by the service index (for example, "3.0.0").
    /// </summary>
    public string? Version { get; set; }

    public bool SupportsPackageMetadata { get; set; } = true;

    public bool SupportsPackageContent { get; set; } = true;

    public bool SupportsReadme { get; set; }

    public bool SupportsRepositorySignatures { get; set; }

    public bool SupportsVulnerabilityInfo { get; set; }

    public bool SupportsSearch { get; set; }

    public bool SupportsAutocomplete { get; set; }

    public bool SupportsSymbolPublish { get; set; }
}

