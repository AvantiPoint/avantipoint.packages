#nullable enable
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Parses signing certificate material returned from cloud key management APIs.
/// </summary>
public static class SigningCertificateParser
{
    /// <summary>
    /// Attempts to create an <see cref="X509Certificate2"/> from PEM-encoded material.
    /// Supports full certificates; public-key-only PEM is not a valid X.509 certificate.
    /// </summary>
    public static X509Certificate2? TryCreateFromPem(string? pemContent)
    {
        if (string.IsNullOrWhiteSpace(pemContent))
        {
            return null;
        }

        try
        {
            return X509Certificate2.CreateFromPem(pemContent);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to create an <see cref="X509Certificate2"/> from DER-encoded bytes.
    /// Handles full certificates; SubjectPublicKeyInfo blobs typically cannot be converted.
    /// </summary>
    public static X509Certificate2? TryCreateFromDer(byte[]? derBytes)
    {
        if (derBytes is null or { Length: 0 })
        {
            return null;
        }

        try
        {
            return X509CertificateLoader.LoadCertificate(derBytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
