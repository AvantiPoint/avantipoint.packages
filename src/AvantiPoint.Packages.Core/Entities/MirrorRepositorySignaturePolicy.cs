namespace AvantiPoint.Packages.Core;

/// <summary>
/// Policy describing how repository signatures from upstream feeds are handled during mirroring.
/// </summary>
public enum MirrorRepositorySignaturePolicy
{
    /// <summary>
    /// Strip any repository signatures and replace them with the local repository certificate.
    /// </summary>
    Resign,

    /// <summary>
    /// Trust upstream repository signatures as-is and record their certificates as trusted.
    /// </summary>
    Merge,

    /// <summary>
    /// Only trust repository signatures that are already present in the trusted set.
    /// Otherwise, strip and re-sign.
    /// </summary>
    TrustedCerts
}

