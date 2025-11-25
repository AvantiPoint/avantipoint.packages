using System;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Signing;

namespace AvantiPoint.Packages.Signing.Gcp;

internal class GcpHsmRepositorySigningKeyProviderServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IRepositorySigningKeyProvider, GcpHsmRepositorySigningKeyProvider>(services),
        IRepositorySigningKeyProviderServiceProvider
{
    public override string Name => SigningProviderNames.GcpHsm;
}


