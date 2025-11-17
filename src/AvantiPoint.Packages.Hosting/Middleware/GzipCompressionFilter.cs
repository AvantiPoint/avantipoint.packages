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
            var gzipStream = new GZipStream(originalBodyStream, CompressionLevel.Optimal, leaveOpen: true);
            httpContext.Response.Body = gzipStream;

            try
            {
                // Execute the endpoint - the JSON will be written to the gzip stream
                var result = await next(context);
                
                // Ensure all data is flushed and finalized
                await gzipStream.FlushAsync();
                
                return result;
            }
            finally
            {
                // Dispose the gzip stream to finalize the compression
                await gzipStream.DisposeAsync();
                
                // Restore the original stream
                httpContext.Response.Body = originalBodyStream;
            }
        }
    }
}
