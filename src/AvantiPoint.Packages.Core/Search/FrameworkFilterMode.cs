namespace AvantiPoint.Packages.Core;

/// <summary>
/// Controls how multiple target-framework filters are combined during package search.
/// </summary>
public enum FrameworkFilterMode
{
    /// <summary>A package may match any selected target framework.</summary>
    Any,

    /// <summary>A package must match every selected target framework.</summary>
    All,
}
