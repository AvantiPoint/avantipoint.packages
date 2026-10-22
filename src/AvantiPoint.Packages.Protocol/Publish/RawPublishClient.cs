using System;
using System.IO;
using System.Net;
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

        private readonly HttpClient _httpClient;
        private readonly ServiceIndexResponse _serviceIndex;

        public RawPublishClient(HttpClient httpClient, ServiceIndexResponse serviceIndex)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _serviceIndex = serviceIndex ?? throw new ArgumentNullException(nameof(serviceIndex));
        }

        public Task<bool> UploadPackageAsync(string packageId, NuGetVersion version, string apiKey,
            Stream packageStream, CancellationToken cancellationToken = default) =>
            UploadAsync(_serviceIndex.GetPackagePublishResourceUrl(), packageId, version,
                "nupkg", apiKey, packageStream, cancellationToken);

        public Task<bool> UploadSymbolsPackageAsync(string packageId, NuGetVersion version, string apiKey,
            Stream packageStream, CancellationToken cancellationToken = default) =>
            UploadAsync(_serviceIndex.GetSymbolPublishResourceUrl(), packageId, version,
                "snupkg", apiKey, packageStream, cancellationToken);

        private async Task<bool> UploadAsync(string uri, string packageId, NuGetVersion version,
            string extension, string apiKey, Stream packageStream, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(uri))
                throw new InvalidOperationException("The feed does not advertise the requested publish resource.");
            ArgumentNullException.ThrowIfNull(packageStream);

            using var request = new HttpRequestMessage(HttpMethod.Put, uri);
            using var multipart = new MultipartFormDataContent();
            using var packageContent = new PackageStreamContent(packageStream, cancellationToken);
            packageContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(packageContent, "package", $"{packageId}.{version.ToNormalizedString()}.{extension}");
            request.Content = multipart;
            if (!string.IsNullOrEmpty(apiKey))
                request.Headers.Add(ApiKeyHeader, apiKey);

            using var response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode;
        }

        // Stream without buffering the package or taking ownership of the caller's stream.
        private sealed class PackageStreamContent(Stream source, CancellationToken cancellationToken) : HttpContent
        {
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) =>
                source.CopyToAsync(stream, cancellationToken);

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context,
                CancellationToken requestCancellationToken) => source.CopyToAsync(stream, requestCancellationToken);

            protected override bool TryComputeLength(out long length)
            {
                length = source.CanSeek ? source.Length - source.Position : 0;
                return source.CanSeek;
            }
        }
    }
}
