#nullable enable

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Marker interface to identify signing providers that do not perform signing.
/// Use this to check if repository signing is disabled: if (provider is INullSigningKeyProvider)
/// </summary>
public interface INullSigningKeyProvider : IRepositorySigningKeyProvider
{
}
