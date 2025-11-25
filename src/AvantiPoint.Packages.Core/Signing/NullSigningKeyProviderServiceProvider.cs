using System;
using AvantiPoint.Packages.Core.Discovery;

namespace AvantiPoint.Packages.Core.Signing;

internal class NullSigningKeyProviderServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IRepositorySigningKeyProvider, NullSigningKeyProvider>(services),
        IRepositorySigningKeyProviderServiceProvider
{
    public override string Name => SigningProviderNames.Null;
}

