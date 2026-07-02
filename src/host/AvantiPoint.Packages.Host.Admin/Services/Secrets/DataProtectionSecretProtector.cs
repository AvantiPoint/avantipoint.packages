using System.Security.Cryptography;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.DataProtection;

namespace AvantiPoint.Packages.Host.Admin.Services.Secrets;

/// <summary>
/// Encrypts stored feed credentials (upstream package source secrets and downstream publish
/// tokens) using ASP.NET Core Data Protection. Protected values carry a versioned marker so
/// legacy plaintext rows pass through <see cref="Unprotect"/> unchanged until the startup
/// migration re-encrypts them.
/// </summary>
/// <remarks>
/// <see cref="IsProtected"/> is a marker check only - it does NOT attempt decryption. This is
/// deliberate: if it instead treated "fails to decrypt" as "not protected", a temporarily
/// wrong/missing Data Protection key ring (e.g. after Host:DataProtection:KeyPath or
/// ApplicationName changes) would make every already-encrypted secret look unprotected, and
/// the startup migration would then encrypt the ciphertext itself as if it were plaintext -
/// permanently destroying the original secret even after the correct key ring is restored.
/// A value carrying <see cref="Marker"/> is therefore never re-encrypted, and a decrypt
/// failure is thrown instead of silently treated as plaintext, so a key-ring/configuration
/// problem is surfaced (and recoverable) instead of causing silent, irreversible data loss.
/// The marker includes a fixed random suffix (not just a short human-readable tag) to make an
/// accidental collision with a real plaintext credential effectively impossible.
/// </remarks>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    internal const string Marker = "dpv1:5b6d9e2a:";
    private const string Purpose = "AvantiPoint.Packages.Host.StoredSecrets.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value) || IsProtected(value))
        {
            // Never re-encrypt a value that already carries the marker - even if it currently
            // fails to decrypt (see remarks). Re-encrypting it would destroy the original secret.
            return value;
        }

        return Marker + _protector.Protect(value);
    }

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value))
        {
            // Legacy plaintext (or empty) - pass through unchanged.
            return value;
        }

        try
        {
            return _protector.Unprotect(value[Marker.Length..]);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Marked as protected but the current key ring can't decrypt it - most likely
            // Host:DataProtection:KeyPath/ApplicationName changed or the key ring was lost.
            // Surface this loudly rather than returning a garbage value that would silently
            // fail authentication downstream (or, worse, get "helpfully" re-encrypted as
            // plaintext and permanently lose the original secret).
            throw new InvalidOperationException(
                "A stored secret could not be decrypted. This usually means the Data Protection " +
                "key ring is missing, or Host:DataProtection:KeyPath/ApplicationName changed. " +
                "Restore the original key ring/configuration before this value can be recovered.",
                ex);
        }
    }

    public bool IsProtected(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith(Marker, StringComparison.Ordinal);
}
