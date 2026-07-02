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
        if (string.IsNullOrEmpty(value) || value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        return Prefix + _protector.Protect(value);
    }

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Legacy plaintext (or empty) — pass through unchanged.
            return value;
        }

        return _protector.Unprotect(value[Prefix.Length..]);
    }

    public bool IsProtected(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith(Prefix, StringComparison.Ordinal);
}
