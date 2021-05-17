using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
            using var content = ToHttpContent(packageId, version, "nupkg", packageStream);

            if (!string.IsNullOrEmpty(apiKey))
                content.Headers.Add(ApiKeyHeader, apiKey);

            using var response = await _httpClient.PostAsync(uri, content, cancellationToken);
            if(!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
            }

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
            using var content = ToHttpContent(packageId, version, "snupkg", packageStream);

            if (!string.IsNullOrEmpty(apiKey))
                content.Headers.Add(ApiKeyHeader, apiKey);

            using var response = await _httpClient.PostAsync(uri, content, cancellationToken);

            return response.IsSuccessStatusCode;
        }

        static HttpContent ToHttpContent(
                string packageId,
                NuGetVersion version,
                string fileExtension,
                Stream packageStream)
        {
            var memoryStream = ConvertToMemoryStream(packageStream);
            var content = new StreamContent(memoryStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"{packageId}.{version.OriginalVersion}.{fileExtension}"
            };

            return content;
        }

        static MemoryStream ConvertToMemoryStream(Stream original)
        {
            var buffer = new byte[16 * 1024];
            var outputStream = new MemoryStream();
            int read;
            while ((read = original.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, read);
            }

            return outputStream;
        }
    }
}
