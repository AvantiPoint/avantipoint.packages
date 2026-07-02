#nullable enable

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Protects secrets (upstream feed credentials, downstream publish tokens) at rest.
/// Implementations must be idempotent: protecting an already-protected value returns it
/// unchanged, and unprotecting a value that was never protected returns it unchanged so
/// legacy plaintext rows keep working until they are migrated.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts the value for storage. Null or empty values are returned unchanged.</summary>
    string? Protect(string? value);

    /// <summary>Decrypts a stored value for use. Values not produced by <see cref="Protect"/> pass through unchanged.</summary>
    string? Unprotect(string? value);

    /// <summary>Whether the stored value is already protected (or has nothing to protect).</summary>
    bool IsProtected(string? value);
}
