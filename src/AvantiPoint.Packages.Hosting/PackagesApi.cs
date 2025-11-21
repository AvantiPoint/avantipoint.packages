using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages
{
    public static class PackagesApi
    {
        public static WebApplication MapNuGetApiRoutes(this WebApplication app)
        {
            // Apply operation cancelled middleware before mapping routes
            app.UseOperationCancelledMiddleware();
            
            return app.MapServiceIndex()
               .MapPackageContentRoutes()
               .MapPackageMetadataRoutes()
               .MapPackagePublishRoutes()
               .MapSearchRoutes()
               .MapShieldRoutes()
               .MapSymbolRoutes()
               .MapVulnerabilityApi()
               .MapRepositorySignaturesApi()
               .MapCertificateDownloadApi();
        }

        public static IServiceCollection AddNuGetApiDocumentation(this IServiceCollection services)
        {
            services.AddOpenApi(options =>
            {
                var path = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.xml")
                    .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == "AvantiPoint.Packages.Hosting");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    options.AddDocumentTransformer((document, context, cancellationToken) =>
                    {
                        // XML comments will be automatically included if XML documentation is enabled in the project
                        return Task.CompletedTask;
                    });
                }
            });
            return services;
        }
    }
}
