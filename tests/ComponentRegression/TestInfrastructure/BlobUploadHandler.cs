using System.Net;
using System.Xml.Linq;

namespace AvantiPoint.Packages.Registry.Oci.Tests.TestInfrastructure;

// Exercises the real Azure SDK uploader without a network connection or credentials.
internal sealed class BlobUploadHandler : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _blocks = [];
    private readonly object _gate = new();
    public byte[] UploadedContent { get; private set; } = [];
    public int Requests { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Put || request.RequestUri?.Host != "storage-fixture.invalid")
            throw new InvalidOperationException("Unexpected fixture request.");

        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
        lock (_gate)
        {
            Requests++;
            if (query["comp"] == "block") _blocks.Add(query["blockid"]!, bytes);
            else if (query["comp"] == "blocklist")
                UploadedContent = XDocument.Load(new MemoryStream(bytes, writable: false)).Root!.Elements()
                    .SelectMany(element => _blocks[element.Value]).ToArray();
            else UploadedContent = bytes;
        }

        var response = new HttpResponseMessage(HttpStatusCode.Created) { Content = new ByteArrayContent([]) };
        response.Headers.TryAddWithoutValidation("ETag", "\"synthetic-etag\"");
        response.Headers.TryAddWithoutValidation("Last-Modified", DateTimeOffset.UtcNow.ToString("R"));
        response.Headers.TryAddWithoutValidation("x-ms-request-id", "synthetic-request");
        response.Headers.TryAddWithoutValidation("x-ms-version", "2025-11-05");
        return response;
    }
}
