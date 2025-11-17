using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Packages.Hosting.Middleware
{
    /// <summary>
    /// Endpoint filter that compresses the response with gzip.
    /// Used for RegistrationsBaseUrl/3.4.0 and RegistrationsBaseUrl/3.6.0 endpoints.
    /// </summary>
    public class GzipCompressionFilter : IEndpointFilter
    {
        public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;
            var originalBodyStream = httpContext.Response.Body;

            using var memoryStream = new MemoryStream();
            httpContext.Response.Body = memoryStream;

            // Execute the endpoint
            var result = await next(context);

            // If the response was successful and has content, compress it
            if (httpContext.Response.StatusCode == 200 && memoryStream.Length > 0)
            {
                memoryStream.Seek(0, SeekOrigin.Begin);

                // Set the Content-Encoding header
                httpContext.Response.Headers["Content-Encoding"] = "gzip";

                // Compress the response
                using var gzipStream = new GZipStream(originalBodyStream, CompressionLevel.Optimal, leaveOpen: true);
                await memoryStream.CopyToAsync(gzipStream);
                await gzipStream.FlushAsync();
            }
            else
            {
                // If no compression is needed, just copy the response
                memoryStream.Seek(0, SeekOrigin.Begin);
                await memoryStream.CopyToAsync(originalBodyStream);
            }

            httpContext.Response.Body = originalBodyStream;
            return result;
        }
    }
}
