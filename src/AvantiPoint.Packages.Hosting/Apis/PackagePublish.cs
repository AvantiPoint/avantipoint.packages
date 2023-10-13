using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Hosting.Internals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Hosting;

internal static class PackagePublish
{
    public static WebApplication MapPackagePublishRoutes(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<PackageFeedOptions>>();

        if(!options.Value.IsReadOnlyMode)
        {
            return app.MapNuGetUpload()
           .MapNuGetDelete()
           .MapNuGetRelist();
        }

        return app;
    }

    private static WebApplication MapNuGetUpload(this WebApplication app)
    {
        app.MapPut("api/v2/package", PutNuGetUpload)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetPublisherFilter>()
           .AddPackageAction<HandlePackageUploadedFilter>(app)
           .WithTags(nameof(PackagePublish))
           .WithName(Routes.UploadPackageRouteName);
        return app;
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(410)]
    [ProducesResponseType(500)]
    private static async ValueTask<IResult> PutNuGetUpload(HttpContext context, IPackageIndexingService indexer, IPackageContext packageContext, CancellationToken cancellationToken)
    {
        try
        {
            using var uploadStream = await context.Request.GetUploadStreamOrNullAsync(cancellationToken);
            if (uploadStream is null || uploadStream.Equals(Stream.Null))
            {
                return Results.BadRequest();
            }

            var result = await indexer.IndexAsync(uploadStream, cancellationToken);

            if(result.Status == PackageIndexingStatus.Success)
            {
                packageContext.PackageId = result.PackageId;
                packageContext.PackageVersion = result.PackageVersion;
                return Results.StatusCode(201);
            }

            return Results.BadRequest();
        }
        catch (ArgumentException ae)
            when (ae.Message.StartsWith("An item with the same key has already been added"))
        {
            // This probably means that package already exists in the database but there is no content stored.
            return Results.StatusCode(410);
        }
        catch (Exception e)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(PackagePublish));
            logger.LogError(e, "Exception thrown during package upload");

            return Results.StatusCode(500);
        }
    }

    private static WebApplication MapNuGetDelete(this WebApplication app)
    {
        app.MapDelete("api/v2/package/{id}/{version}", DeletePackage)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetPublisherFilter>()
           .WithTags(nameof(PackagePublish))
           .WithName(Routes.DeleteRouteName);

        return app;
    }

    [ProducesResponseType(typeof(NoContentResult), 204)]
    [ProducesResponseType(typeof(NotFoundResult), 404)]
    private static async ValueTask<IResult> DeletePackage(string id, string version, IPackageDeletionService deletionService, CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }

        if (await deletionService.TryDeletePackageAsync(id, nugetVersion, cancellationToken))
        {
            return Results.NoContent();
        }
        else
        {
            return Results.NotFound();
        }
    }

    private static WebApplication MapNuGetRelist(this WebApplication app)
    {
        app.MapPost("api/v2/package/{id}/{version}", PostRelist)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetPublisherFilter>()
           .WithTags(nameof(PackagePublish))
           .WithName(Routes.RelistRouteName);

        return app;
    }

    [ProducesResponseType(typeof(OkResult), 200)]
    [ProducesResponseType(typeof(NotFoundResult), 404)]
    private static async ValueTask<IResult> PostRelist(string id, string version, IPackageService packages, CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }
        else if (await packages.RelistPackageAsync(id, nugetVersion, cancellationToken))
        {
            return Results.Ok();
        }

        return Results.NotFound();
    }
}
