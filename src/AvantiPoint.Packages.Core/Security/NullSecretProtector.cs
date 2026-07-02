#nullable enable

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Pass-through protector used when the application has not registered an encrypting
/// implementation (for example lightweight embedded scenarios). Stores secrets as-is.
/// </summary>
public sealed class NullSecretProtector : ISecretProtector
{
    public string? Protect(string? value) => value;

    public string? Unprotect(string? value) => value;

    public bool IsProtected(string? value) => true;
}
