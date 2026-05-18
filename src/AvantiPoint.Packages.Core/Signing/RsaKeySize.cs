namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Industry-standard RSA key sizes for certificate generation.
/// </summary>
public enum RsaKeySize
{
    /// <summary>
    /// 2048-bit key size (minimum recommended).
    /// </summary>
    KeySize2048 = 2048,

    /// <summary>
    /// 3072-bit key size (intermediate security).
    /// </summary>
    KeySize3072 = 3072,

    /// <summary>
    /// 4096-bit key size (high security, default).
    /// </summary>
    KeySize4096 = 4096
}

