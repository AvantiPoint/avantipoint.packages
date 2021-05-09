using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Protocol
{
    public partial class NuGetClientFactory
    {
        public class PublishClient : IPublishClient
        {
            private readonly NuGetClientFactory _clientfactory;

            public PublishClient(NuGetClientFactory clientFactory)
            {
                _clientfactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            }

            public async Task<bool> UploadPackageAsync(
                    string packageId,
                    string version,
                    Uri packageSource,
                    string apiKey,
                    Stream packageStream,
                    CancellationToken cancellationToken = default)
            {
                var client = await _clientfactory.GetPublishClientAsync(cancellationToken);

                return await client.UploadPackageAsync(packageId, version, packageSource, apiKey, packageStream, cancellationToken);
            }

            public async Task<bool> UploadSymbolsPackageAsync(
                    string packageId,
                    string version,
                    Uri packageSource,
                    string apiKey,
                    Stream packageStream,
                    CancellationToken cancellationToken = default)
            {
                var client = await _clientfactory.GetPublishClientAsync(cancellationToken);

                return await client.UploadSymbolsPackageAsync(packageId, version, packageSource, apiKey, packageStream, cancellationToken);
            }
        }
    }
}
