using System;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Signing;

namespace AvantiPoint.Packages.Signing.Aws;

internal class AwsSignerRepositorySigningKeyProviderServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IRepositorySigningKeyProvider, AwsSignerRepositorySigningKeyProvider>(services),
        IRepositorySigningKeyProviderServiceProvider
{
    public override string Name => SigningProviderNames.AwsSigner;
}


