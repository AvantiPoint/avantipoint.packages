using System;
using System.IO;
using System.Linq;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AvantiPoint.Packages
{
    public static class PackagesApi
    {
        public static WebApplication MapNuGetApiRoutes(this WebApplication app) =>
            app.MapServiceIndex()
               .MapPackageContentRoutes()
               .MapPackageMetadataRoutes()
               .MapPackagePublishRoutes()
               .MapSearchRoutes()
               .MapShieldRoutes()
               .MapSymbolRoutes();

        public static void IncludeNuGetApi(this SwaggerGenOptions options)
        {
            var path = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.xml")
                .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == "AvantiPoint.Packages.Hosting");
            if (!string.IsNullOrWhiteSpace(path))
                options.IncludeXmlComments(path);
        }
    }
}
