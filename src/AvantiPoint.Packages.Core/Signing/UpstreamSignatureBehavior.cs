namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Defines the behavior when a package already has a repository signature (e.g., from nuget.org).
/// </summary>
public enum UpstreamSignatureBehavior
{
    /// <summary>
    /// Strip the existing repository signature and replace it with our own.
    /// Author signatures are preserved when stripping repository signatures.
    /// This is the default behavior.
    /// </summary>
    ReSign = 0,

    /// <summary>
    /// Reject packages that already have repository signatures.
    /// Package uploads will fail with an error.
    /// </summary>
    Reject = 1
}

