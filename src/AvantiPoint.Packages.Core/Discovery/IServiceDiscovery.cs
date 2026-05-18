#nullable enable
using AvantiPoint.Packages.Core.Signing;

namespace AvantiPoint.Packages.Core.Discovery;

internal interface IServiceDiscovery
{
    IContext GetContext(string? name = null);
    IRepositorySigningKeyProvider GetSigningKeyProvider(string? name = null);
    IStorageService GetStorageService(string? name = null);
}