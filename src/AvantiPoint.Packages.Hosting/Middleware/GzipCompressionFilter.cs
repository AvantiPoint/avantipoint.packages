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
            
            // Set the Content-Encoding header before the response is written
            httpContext.Response.Headers.ContentEncoding = "gzip";
            
            // Replace the response body stream with a GZipStream
            var originalBodyStream = httpContext.Response.Body;
            await using var gzipStream = new GZipStream(originalBodyStream, CompressionLevel.Optimal, leaveOpen: true);
            httpContext.Response.Body = gzipStream;

            try
            {
                // Execute the endpoint - the JSON will be written to the gzip stream
                return await next(context);
            }
            finally
            {
                // Restore the original stream
                httpContext.Response.Body = originalBodyStream;
            }
        }
    }
}
