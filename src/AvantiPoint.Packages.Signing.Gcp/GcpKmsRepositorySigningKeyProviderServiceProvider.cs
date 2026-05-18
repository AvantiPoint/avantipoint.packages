using System;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Signing;

namespace AvantiPoint.Packages.Signing.Gcp;

internal class GcpKmsRepositorySigningKeyProviderServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IRepositorySigningKeyProvider, GcpKmsRepositorySigningKeyProvider>(services),
        IRepositorySigningKeyProviderServiceProvider
{
    public override string Name => SigningProviderNames.GcpKms;
}


