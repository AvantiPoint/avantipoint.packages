namespace SampleDataGenerator;

/// <summary>
/// Defines a package to download from NuGet.org
/// </summary>
public record PackageDefinition
{
    /// <summary>
    /// The package ID
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// The maximum number of versions to download (takes the latest versions)
    /// </summary>
    public int MaxVersions { get; init; } = 3;

    /// <summary>
    /// Whether to include prerelease versions
    /// </summary>
    public bool IncludePrerelease { get; init; } = true;
}
