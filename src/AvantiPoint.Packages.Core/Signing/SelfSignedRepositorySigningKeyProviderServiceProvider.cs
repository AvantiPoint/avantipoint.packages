using System;
using AvantiPoint.Packages.Core.Discovery;

namespace AvantiPoint.Packages.Core.Signing;

internal class SelfSignedRepositorySigningKeyProviderServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IRepositorySigningKeyProvider, SelfSignedRepositorySigningKeyProvider>(services),
        IRepositorySigningKeyProviderServiceProvider
{
    public override string Name => SigningProviderNames.SelfSigned;
}


