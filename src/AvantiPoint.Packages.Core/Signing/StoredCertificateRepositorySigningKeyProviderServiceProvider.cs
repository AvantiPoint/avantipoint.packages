using System;
using AvantiPoint.Packages.Core.Discovery;

namespace AvantiPoint.Packages.Core.Signing;

internal class StoredCertificateRepositorySigningKeyProviderServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IRepositorySigningKeyProvider, StoredCertificateRepositorySigningKeyProvider>(services),
        IRepositorySigningKeyProviderServiceProvider
{
    public override string Name => SigningProviderNames.StoredCertificate;
}


