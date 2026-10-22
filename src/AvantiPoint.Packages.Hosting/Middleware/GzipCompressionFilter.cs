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
            var result = await next(context);
            return result is IResult httpResult
                ? new GzipResult(httpResult)
                : result;
        }

        private sealed class GzipResult(IResult inner) : IResult
        {
            public async Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.Headers.ContentEncoding = "gzip";

                var originalBodyStream = httpContext.Response.Body;
                await using var gzipStream = new GZipStream(
                    originalBodyStream,
                    CompressionLevel.Optimal,
                    leaveOpen: true);
                httpContext.Response.Body = gzipStream;

                try
                {
                    await inner.ExecuteAsync(httpContext);
                }
                finally
                {
                    httpContext.Response.Body = originalBodyStream;
                }
            }
        }
    }
}
