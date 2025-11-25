using System;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Signing;

namespace AvantiPoint.Packages.Signing.Aws;

internal class AwsKmsRepositorySigningKeyProviderServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IRepositorySigningKeyProvider, AwsKmsRepositorySigningKeyProvider>(services),
        IRepositorySigningKeyProviderServiceProvider
{
    public override string Name => SigningProviderNames.AwsKms;
}


