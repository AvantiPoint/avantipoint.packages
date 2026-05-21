#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace AvantiPoint.Packages.Signing.Gcp;

public enum GcpProtectionLevel
{
    /// <summary>
    /// Software protection (keys stored in software).
    /// </summary>
    Software,

    /// <summary>
    /// Hardware Security Module protection (FIPS 140-2 Level 3 validated).
    /// </summary>
    Hsm
}

