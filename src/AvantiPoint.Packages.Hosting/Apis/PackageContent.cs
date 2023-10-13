using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Hosting.Internals;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Hosting;

internal static class PackageContent
{
    public static WebApplication MapPackageContentRoutes(this WebApplication app) =>
        app.MapGetPackageVersions()
           .MapDownloadPackage()
           .MapDownloadNuSpec()
           .MapDownloadReadMe();

    private static WebApplication MapGetPackageVersions(this WebApplication app)
    {
        app.MapGet("v3/package/{id}/index.json", GetPackageVersions)
            .AllowAnonymous()
            .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
            .WithTags(nameof(PackageContent))
            .WithName(Routes.PackageVersionsRouteName);

        return app;
    }

    /// <summary>
    /// Gets a list of available versions for a specified Package Id
    /// </summary>
    /// <param name="id">The Package Id to retrieve packages for.</param>
    /// <param name="content"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>A list of Package Versions</returns>
    /// <response code="200">A list of Package Versions.</response>
    /// <response code="404">The no package exists with the specified Package Id.</response>
    [ProducesResponseType(typeof(PackageVersionsResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404, "application/json")]
    private static async ValueTask<IResult> GetPackageVersions(string id, IPackageContentService content, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var versions = await content.GetPackageVersionsOrNullAsync(id, cancellationToken);
        if (versions == null)
        {
            return Results.NotFound();
        }

        loggerFactory.Logger().LogInformation("Getting package versions: {Id}", id);
        return Results.Ok(versions);
    }

    private static WebApplication MapDownloadPackage(this WebApplication app)
    {
        app.MapGet("v3/package/{id}/{version}/{idVersion}.nupkg", DownloadPackage)
            .AllowAnonymous()
            .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
            .AddPackageAction<HandlePackageDownloadedFilter>(app)
            .WithTags(nameof(PackageContent))
            .WithName(Routes.PackageDownloadRouteName);

        return app;
    }

    /// <summary>
    /// Downloads a specified Package by Id & Version.
    /// </summary>
    /// <param name="id">Package Id of the Package to download.</param>
    /// <param name="version">Package Version of the Package to download.</param>
    /// <param name="content"></param>
    /// <param name="packageContext"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <response code="200">The NuGet Package file.</response>
    [ProducesResponseType(typeof(FileStreamResult), 200, "application/octet-stream")]
    [ProducesResponseType(typeof(StatusCodeResult), 404, "application/json")]
    private static async ValueTask<IResult> DownloadPackage(string id, string version, IPackageContentService content, IPackageContext packageContext, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }

        using var packageStream = await content.GetPackageContentStreamOrNullAsync(id, nugetVersion, cancellationToken);
        if (packageStream == null)
        {
            return Results.NotFound();
        }

        packageContext.PackageId = id;
        packageContext.PackageVersion = version;

        loggerFactory.Logger().LogInformation("Downloading Package: {Id} {Version}", id, version);
        return Results.File(packageStream.AsMemoryStream().ToArray(), "application/octet-stream", fileDownloadName: $"{id}.{version}.nupkg");
    }

    private static WebApplication MapDownloadNuSpec(this WebApplication app)
    {
        app.MapGet("v3/package/{id}/{version}/{id2}.nuspec", DownloadNuSpec)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .WithTags(nameof(PackageContent))
           .WithName(Routes.PackageDownloadManifestRouteName);
        return app;
    }

    /// <summary>
    /// Downloads the NuGet Package NuSpec file
    /// </summary>
    /// <param name="id">The Package Id of the Package to get the NuSpec for.</param>
    /// <param name="version">The Package Version of the Package to get the NuSpec for.</param>
    /// <param name="content"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The Package NuSpec.</returns>
    /// <response code="200">The NuSpec file.</response>
    [ProducesResponseType(typeof(FileStreamResult), 200, "text/xml")]
    [ProducesResponseType(typeof(StatusCodeResult), 404, "application/json")]
    private static async ValueTask<IResult> DownloadNuSpec(string id, string version, IPackageContentService content, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }

        var nuspecStream = await content.GetPackageManifestStreamOrNullAsync(id, nugetVersion, cancellationToken);
        if (nuspecStream == null)
        {
            return Results.NotFound();
        }

        loggerFactory.Logger().LogInformation("Downloading NuSpec: {Id} {Version}", id, version);
        return Results.File(nuspecStream, "text/xml", fileDownloadName: $"{id}.nuspec");
    }

    private static WebApplication MapDownloadReadMe(this WebApplication app)
    {
        app.MapGet("v3/package/{id}/{version}/readme", DownloadReadMe)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .WithTags(nameof(PackageContent))
           .WithName(Routes.PackageDownloadReadmeRouteName);
        return app;
    }

    /// <summary>
    /// Downloads the Package ReadMe
    /// </summary>
    /// <param name="id">The Package Id.</param>
    /// <param name="version">The Package Version.</param>
    /// <param name="content"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <response code="200">Downloads the Package ReadMe</response>
    [ProducesResponseType(typeof(FileStreamResult), 200, "text/markdown")]
    [ProducesResponseType(typeof(StatusCodeResult), 404, "application/json")]
    private static async ValueTask<IResult> DownloadReadMe(string id, string version, IPackageContentService content, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if(!NuGetVersion.TryParse(version, out var nugetVersion))
            {
            return Results.NotFound();
        }

        var readmeStream = await content.GetPackageReadmeStreamOrNullAsync(id, nugetVersion, cancellationToken);
        if (readmeStream == null)
        {
            return Results.NotFound();
        }

        loggerFactory.Logger().LogInformation("ReadMe Download: {Id} {Version}", id, version);
        return Results.File(readmeStream, "text/markdown", fileDownloadName: "ReadMe.md");
    }

    private static WebApplication MapDownloadIcon(this WebApplication app)
    {
        app.MapGet("v3/package/{id}/{version}/icon", DownloadIcon)
           .AllowAnonymous()
           .WithTags(nameof(PackageContent))
           .WithName(Routes.PackageDownloadIconRouteName);
        return app;
    }

    /// <summary>
    /// Downloads the Package Icon for the specified package id and version
    /// </summary>
    /// <param name="id">The Package Id.</param>
    /// <param name="version">The Package Version.</param>
    /// <param name="content"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(FileStreamResult), 200, "image/xyz")]
    [ProducesResponseType(typeof(StatusCodeResult), 404, "application/json")]
    private static async ValueTask<IResult> DownloadIcon(string id, string version, IPackageContentService content, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }

        var iconStream = await content.GetPackageIconStreamOrNullAsync(id, nugetVersion, cancellationToken);
        if (iconStream == null)
        {
            return Results.NotFound();
        }

        return Results.File(iconStream, "image/xyz");
    }

    private static ILogger Logger(this ILoggerFactory loggerFactory) =>
        loggerFactory.CreateLogger(nameof(PackageContent));
}
