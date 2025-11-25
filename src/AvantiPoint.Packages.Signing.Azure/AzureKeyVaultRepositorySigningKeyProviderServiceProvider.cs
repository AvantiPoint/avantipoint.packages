using System;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Signing;

namespace AvantiPoint.Packages.Signing.Azure;

internal class AzureKeyVaultRepositorySigningKeyProviderServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IRepositorySigningKeyProvider, AzureKeyVaultRepositorySigningKeyProvider>(services),
        IRepositorySigningKeyProviderServiceProvider
{
    public override string Name => SigningProviderNames.AzureKeyVault;
}


