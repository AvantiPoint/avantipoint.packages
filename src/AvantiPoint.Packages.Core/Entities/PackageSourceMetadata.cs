namespace AvantiPoint.Packages.Core;

/// <summary>
/// Captures supplemental information about an upstream source that is discovered at runtime.
/// </summary>
public class PackageSourceMetadata
{
    public PackageSourceMetadataProtocol Protocol { get; set; } = new();

    /// <summary>
    /// Optional arbitrary notes describing the source configuration.
    /// </summary>
    public string Notes { get; set; }
}

