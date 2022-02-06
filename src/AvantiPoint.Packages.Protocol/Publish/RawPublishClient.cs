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
                Stream packageStream,
                CancellationToken cancellationToken = default)
        {
            var uri = _serviceIndex.GetPackagePublishResourceUrl();
            using var content = ToHttpContent(packageId, version, "nupkg", packageStream);

            using var response = await _httpClient.PostAsync(uri, content, cancellationToken);
            if(!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(responseBody))
                    responseBody = $"The HttpClient responded with status code: {response.StatusCode}";

                throw new Exception(responseBody);
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UploadSymbolsPackageAsync(
                string packageId,
                NuGetVersion version,
                Stream packageStream,
                CancellationToken cancellationToken = default)
        {
            var uri = _serviceIndex.GetSymbolPublishResourceUrl();
            using var content = ToHttpContent(packageId, version, "snupkg", packageStream);

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
            memoryStream.Seek(0, SeekOrigin.Begin);
            var content = new StreamContent(memoryStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");
            content.Headers.ContentDisposition.Name = "\"firmfile\"";
            content.Headers.ContentDisposition.FileName = @$"""{packageId}.{version.OriginalVersion}.{fileExtension}""";
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            string boundary = Guid.NewGuid().ToString();
            var formContent = new MultipartFormDataContent(boundary);
            content.Headers.Remove("Content-Type");
            content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);
            formContent.Add(content);

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
