#nullable enable

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Configures read-through access to a NuGet global packages folder.
/// </summary>
public sealed class LocalCacheOptions
{
    /// <summary>
    /// Enables reads from the local NuGet global packages folder before configured upstream sources.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The global packages folder to read. When omitted, the service uses <c>NUGET_PACKAGES</c>
    /// and then falls back to <c>~/.nuget/packages</c>.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Copies packages found in the global packages folder into feed storage. The copied package
    /// uses cache-only ingestion semantics and is not written to the database or search index.
    /// </summary>
    public bool CopyToFeedStorage { get; set; }
}
