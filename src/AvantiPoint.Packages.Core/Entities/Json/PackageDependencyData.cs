namespace AvantiPoint.Packages.Core.Entities.Json;

/// <summary>
/// Helper class for deserializing package dependencies from JSON.
/// </summary>
internal class PackageDependencyData
{
    public string? Id { get; set; }
    public string? VersionRange { get; set; }
    public string? TargetFramework { get; set; }
}
