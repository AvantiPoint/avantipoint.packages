using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Hosting.Internals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Hosting;

internal static class Symbol
{
    public static WebApplication MapSymbolRoutes(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<PackageFeedOptions>>();
        if (!options.Value.IsReadOnlyMode)
            app.MapUploadSymbols();

        app.MapGetSymbols();
        return app;
    }

    // See: https://docs.microsoft.com/en-us/nuget/api/package-publish-resource#push-a-package
    private static WebApplication MapUploadSymbols(this WebApplication app)
    {
        app.MapPut("api/v2/symbol", PutUploadSymbols)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetPublisherFilter>()
           .AddPackageAction<HandleSymbolsUploadedFilter>(app)
           .WithTags(nameof(Symbol))
           .WithName(Routes.UploadSymbolRouteName);
        return app;
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(BadRequestResult), 400)]
    [ProducesResponseType(typeof(NotFoundResult), 404)]
    [ProducesResponseType(500)]
    private static async ValueTask<IResult> PutUploadSymbols(HttpContext context, ISymbolIndexingService indexer, IPackageContext packageContext, CancellationToken cancellationToken)
    {
        try
        {
            using var uploadStream = await context.Request.GetUploadStreamOrNullAsync(cancellationToken);
            if (uploadStream == null)
            {
                return Results.BadRequest();
            }

            var result = await indexer.IndexAsync(uploadStream, cancellationToken);

            switch (result.Status)
            {
                case SymbolIndexingStatus.InvalidSymbolPackage:
                    return Results.BadRequest();

                case SymbolIndexingStatus.PackageNotFound:
                    return Results.NotFound();

                case SymbolIndexingStatus.Success:
                    packageContext.PackageId = result.PackageId;
                    packageContext.PackageVersion = result.PackageVersion;
                    return Results.StatusCode(201);
            }

            return Results.BadRequest();
        }
        catch (Exception e)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(PutUploadSymbols));
            logger.LogError(e, "Exception thrown during symbol upload");

            return Results.StatusCode(500);
        }
    }

    private static WebApplication MapGetSymbols(this WebApplication app)
    {
        app.MapGet("api/download/symbols/{file}/{key}/{file2}", GetSymbols)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .AddPackageAction<HandleSymbolsDownloadedFilter>(app)
           .WithTags(nameof(Symbol))
           .WithName(Routes.SymbolDownloadRouteName);

        app.MapGet("api/download/symbols/{prefix}/{file}/{key}/{file2}", GetSymbolsWithPrefix)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .AddPackageAction<HandleSymbolsDownloadedFilter>(app)
           .WithTags(nameof(Symbol))
           .WithName(Routes.PrefixedSymbolDownloadRouteName);

        return app;
    }

    [ProducesResponseType(typeof(FileResult), 200, "application/octet-stream")]
    [ProducesResponseType(typeof(NotFoundResult), 404)]
    private static async ValueTask<IResult> GetSymbols(string file, string key, string file2, ISymbolStorageService storage)
    {
        using var pdbStream = await storage.GetPortablePdbContentStreamOrNullAsync(file, key);
        if (pdbStream == null)
        {
            return Results.NotFound();
        }

        return Results.File(pdbStream.AsMemoryStream(), "application/octet-stream");
    }

    [ProducesResponseType(typeof(FileResult), 200, "application/octet-stream")]
    [ProducesResponseType(typeof(NotFoundResult), 404)]
    private static async ValueTask<IResult> GetSymbolsWithPrefix(string prefix, string file, string key, string file2, ISymbolStorageService storage)
    {
        using var pdbStream = await storage.GetPortablePdbContentStreamOrNullAsync(file, key);
        if (pdbStream == null)
        {
            return Results.NotFound();
        }

        return Results.File(pdbStream.AsMemoryStream(), "application/octet-stream");
    }
}
