using System.Security.Cryptography;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.DataProtection;

namespace AvantiPoint.Packages.Host.Admin.Services.Secrets;

/// <summary>
/// Encrypts stored feed credentials (upstream package source secrets and downstream publish
/// tokens) using ASP.NET Core Data Protection. Protected values carry a versioned prefix so
/// legacy plaintext rows pass through <see cref="Unprotect"/> unchanged until the startup
/// migration re-encrypts them.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    internal const string Prefix = "dpv1:";
    private const string Purpose = "AvantiPoint.Packages.Host.StoredSecrets.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value) || TryUnprotect(value, out _))
        {
            // Already a genuinely protected payload - avoid double-encrypting.
            return value;
        }

        return Prefix + _protector.Protect(value);
    }

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Legacy plaintext (including plaintext that happens to start with Prefix - see
        // TryUnprotect) passes through unchanged.
        return TryUnprotect(value, out var plaintext) ? plaintext : value;
    }

    public bool IsProtected(string? value) =>
        string.IsNullOrEmpty(value) || TryUnprotect(value, out _);

    /// <summary>
    /// Attempts to treat <paramref name="value"/> as a genuine protected payload. A value is
    /// only genuinely protected when it carries <see cref="Prefix"/> AND the remainder
    /// successfully decrypts - a legacy plaintext secret that coincidentally starts with
    /// <see cref="Prefix"/> fails decryption and is correctly treated as plaintext instead of
    /// being skipped by the migration and later failing authentication.
    /// </summary>
    private bool TryUnprotect(string value, out string? plaintext)
    {
        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            plaintext = null;
            return false;
        }

        try
        {
            // A value that merely starts with Prefix but isn't real ciphertext can fail either
            // at base64 decoding (FormatException) or during unprotection (CryptographicException).
            plaintext = _protector.Unprotect(value[Prefix.Length..]);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            plaintext = null;
            return false;
        }
    }
}
