#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Caches a repository signing certificate for a bounded period to reduce repeated cloud API calls.
/// </summary>
public sealed class RepositorySigningCertificateCache
{
    private readonly object _sync = new();
    private X509Certificate2? _certificate;
    private bool _populated;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Default cache lifetime used by cloud signing key providers.
    /// </summary>
    public static TimeSpan DefaultLifetime { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Returns a cached certificate when the entry is still valid.
    /// </summary>
    /// <param name="certificate">The cached certificate, which may be null when negative-cached.</param>
    /// <returns><c>true</c> when a valid cache entry exists; otherwise <c>false</c>.</returns>
    public bool TryGet(out X509Certificate2? certificate)
    {
        lock (_sync)
        {
            if (_populated && DateTimeOffset.UtcNow < _expiresAt)
            {
                certificate = _certificate;
                return true;
            }
        }

        certificate = null;
        return false;
    }

    /// <summary>
    /// Stores a certificate (or null) in the cache.
    /// </summary>
    public void Set(X509Certificate2? certificate, TimeSpan? lifetime = null)
    {
        lock (_sync)
        {
            _certificate = certificate;
            _populated = true;
            _expiresAt = DateTimeOffset.UtcNow.Add(lifetime ?? DefaultLifetime);
        }
    }
}
