namespace AvantiPoint.Packages.Core;

/// <summary>
/// Controls how a page of local and upstream NuGet search results is combined.
/// </summary>
public enum FederatedSearchMergeStrategy
{
    /// <summary>
    /// Return every result from every source, including repeated package IDs.
    /// </summary>
    Union,

    /// <summary>
    /// Return one result per package ID and select the result with the highest package version.
    /// </summary>
    Deduplicate,

    /// <summary>
    /// Return one result per package ID and retain the local result when the ID exists locally.
    /// </summary>
    LocalPreferred,
}
