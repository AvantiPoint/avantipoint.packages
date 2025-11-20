using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AvantiPoint.Packages.Tests.Signing;

/// <summary>
/// Helper class for generating test certificates for signing tests.
/// </summary>
public static class TestCertificateHelper
{
    /// <summary>
    /// Creates a self-signed test certificate for use in unit tests.
    /// </summary>
    public static X509Certificate2 CreateTestCertificate(
        string subjectName = "CN=Test Certificate",
        int keySize = 2048,
        int validityDays = 365,
        HashAlgorithmName? hashAlgorithm = null)
    {
        hashAlgorithm ??= HashAlgorithmName.SHA256;

        using var rsa = RSA.Create(keySize);

        var request = new CertificateRequest(
            subjectName,
            rsa,
            hashAlgorithm.Value,
            RSASignaturePadding.Pkcs1);

        // Add key usage for digital signature
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        // Add basic constraints (not a CA)
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(validityDays);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>
    /// Creates an expired test certificate.
    /// </summary>
    public static X509Certificate2 CreateExpiredCertificate(string subjectName = "CN=Expired Certificate")
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        // Certificate expired 30 days ago
        var notBefore = DateTimeOffset.UtcNow.AddDays(-60);
        var notAfter = DateTimeOffset.UtcNow.AddDays(-30);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>
    /// Creates a certificate that will expire soon (within 7 days).
    /// </summary>
    public static X509Certificate2 CreateExpiringSoonCertificate(string subjectName = "CN=Expiring Certificate")
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        // Certificate expires in 5 days
        var notBefore = DateTimeOffset.UtcNow.AddDays(-30);
        var notAfter = DateTimeOffset.UtcNow.AddDays(5);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>
    /// Creates a certificate that is not yet valid.
    /// </summary>
    public static X509Certificate2 CreateNotYetValidCertificate(string subjectName = "CN=Future Certificate")
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        // Certificate becomes valid in 10 days
        var notBefore = DateTimeOffset.UtcNow.AddDays(10);
        var notAfter = notBefore.AddDays(365);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>
    /// Computes the SHA-256 fingerprint of a certificate (lowercase hex).
    /// </summary>
    public static string ComputeSha256Fingerprint(X509Certificate2 certificate)
    {
        return Convert.ToHexString(SHA256.HashData(certificate.RawData)).ToLowerInvariant();
    }

    /// <summary>
    /// Computes the SHA-384 fingerprint of a certificate (lowercase hex).
    /// </summary>
    public static string ComputeSha384Fingerprint(X509Certificate2 certificate)
    {
        return Convert.ToHexString(SHA384.HashData(certificate.RawData)).ToLowerInvariant();
    }

    /// <summary>
    /// Computes the SHA-512 fingerprint of a certificate (lowercase hex).
    /// </summary>
    public static string ComputeSha512Fingerprint(X509Certificate2 certificate)
    {
        return Convert.ToHexString(SHA512.HashData(certificate.RawData)).ToLowerInvariant();
    }
}
