using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Host.Admin.Services.Secrets;

/// <summary>
/// Helpers for safely applying optional secret fields from administrative forms.
/// </summary>
public static class SecretUpdateExtensions
{
    /// <summary>
    /// Returns the submitted plaintext value when one was supplied. A blank submission keeps
    /// the existing stored value and decrypts it before the entity is passed back through the
    /// normal protect-on-save path.
    /// </summary>
    public static string? ResolveSecretUpdate(
        this ISecretProtector protector,
        string? submittedValue,
        string? existingStoredValue) =>
        string.IsNullOrWhiteSpace(submittedValue)
            ? protector.Unprotect(existingStoredValue)
            : submittedValue;
}
