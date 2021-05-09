using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol
{
    public class RawPublishClient : IPublishClient
    {
        public const string ApiKeyHeader = "X-NuGet-ApiKey";

        private HttpClient _httpClient { get; }
        private ServiceIndexResponse _serviceIndex { get; }

        public RawPublishClient(HttpClient httpClient, ServiceIndexResponse serviceIndex)
        {
            _httpClient = httpClient;
            _serviceIndex = serviceIndex;
        }

        public async Task<bool> UploadPackageAsync(
                string packageId,
                NuGetVersion version,
                string apiKey,
                Stream packageStream,
                CancellationToken cancellationToken = default)
        {
            var uri = _serviceIndex.GetPackagePublishResourceUrl();
            using var content = new MultipartFormDataContent
            {
                { new StreamContent(packageStream), "application/octet-stream", $"{packageId}.{version.OriginalVersion}.snupkg" }
            };

            if (!string.IsNullOrEmpty(apiKey))
                content.Headers.Add(ApiKeyHeader, apiKey);

            using var response = await _httpClient.PostAsync(uri, content, cancellationToken);

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UploadSymbolsPackageAsync(
                string packageId,
                NuGetVersion version,
                string apiKey,
                Stream packageStream,
                CancellationToken cancellationToken = default)
        {
            var uri = _serviceIndex.GetSymbolPublishResourceUrl();
            using var content = new MultipartFormDataContent
            {
                { new StreamContent(packageStream), "application/octet-stream", $"{packageId}.{version.OriginalVersion}.snupkg" }
            };

            if (!string.IsNullOrEmpty(apiKey))
                content.Headers.Add(ApiKeyHeader, apiKey);

            using var response = await _httpClient.PostAsync(uri, content, cancellationToken);

            return response.IsSuccessStatusCode;
        }
    }
}
