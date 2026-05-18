#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Helper class for validating repository signing certificates.
/// </summary>
public class CertificateValidationHelper(TimeProvider timeProvider)
{
    /// <summary>
    /// Minimum validity period required for a certificate to be considered valid.
    /// This buffer prevents issues where a certificate is valid when checked but expires during signing.
    /// </summary>
    public static readonly TimeSpan MinimumValidityPeriod = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Checks if a certificate is expired or will expire within the minimum validity period.
    /// </summary>
    /// <param name="certificate">The certificate to validate.</param>
    /// <returns>True if the certificate is expired or will expire within the minimum validity period; otherwise, false.</returns>
    public bool IsCertificateExpired(X509Certificate2 certificate)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var minimumValidUntil = now.Add(MinimumValidityPeriod);

        // Certificate must be valid now and remain valid for at least the minimum period.
        var certNotAfter = ToUtcDateTime(certificate.NotAfter);
        var certNotBefore = ToUtcDateTime(certificate.NotBefore);
        return certNotAfter < minimumValidUntil || certNotBefore > now;
    }

    /// <summary>
    /// Gets the time remaining until the certificate expires.
    /// </summary>
    /// <param name="certificate">The certificate to check.</param>
    /// <returns>The time remaining until expiration, or a negative value if already expired.</returns>
    public TimeSpan GetTimeUntilExpiry(X509Certificate2 certificate)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var certNotAfter = ToUtcDateTime(certificate.NotAfter);
        return certNotAfter - now;
    }

    /// <summary>
    /// Normalizes X.509 validity timestamps to UTC. Unspecified kind is treated as UTC
    /// (RFC 5280 validity times are UTC); Local is converted from local time.
    /// </summary>
    private static DateTime ToUtcDateTime(DateTime dateTime) =>
        dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
        };
}

