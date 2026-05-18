namespace AvantiPoint.Packages.Core;

/// <summary>
/// Identifies how a package entered this feed. Matches the values tracked for origin metadata.
/// </summary>
public enum PackageOrigin
{
    /// <summary>
    /// Package was published directly to this feed via push, CLI, or UI.
    /// </summary>
    Published,

    /// <summary>
    /// Package was mirrored from an upstream source and is indexed locally.
    /// </summary>
    Mirrored,

    /// <summary>
    /// Package was cached from an upstream source for restore scenarios but is not indexed.
    /// </summary>
    Cached
}

