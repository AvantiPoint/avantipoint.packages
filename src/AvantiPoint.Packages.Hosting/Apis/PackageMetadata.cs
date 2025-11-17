using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;
using AvantiPoint.Packages.Hosting.Caching;

namespace AvantiPoint.Packages.Hosting;

internal static class PackageMetadata
{
    public static WebApplication MapPackageMetadataRoutes(this WebApplication app) =>
        app.MapRegistrationIndex()
           .MapRegistrationLeaf()
           .MapRegistrationIndexGzSemVer1()
           .MapRegistrationLeafGzSemVer1()
           .MapRegistrationIndexGzSemVer2()
           .MapRegistrationLeafGzSemVer2();

    private static WebApplication MapRegistrationIndex(this WebApplication app)
    {
        app.MapGet("v3/registration/{id}/index.json", GetRegistrationIndex)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .UseNugetCaching()
           .WithTags(nameof(PackageMetadata))
           .WithName(Routes.RegistrationIndexRouteName);
        return app;
    }

    /// <summary>
    /// Get's the Registration Index for a specified Package Id
    /// </summary>
    /// <param name="id">The Package Id</param>
    /// <param name="_metadata"></param>
    /// <param name="semVerLevel">SemVer level (2.0.0 to include SemVer2 packages)</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <response code="200">Returns the Api Registration Index response</response>
    /// <response code="404">No package was found.</response>
    [Produces("application/json")]
    [ProducesResponseType(typeof(NuGetApiRegistrationIndexResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404)]
    private static async ValueTask<IResult> GetRegistrationIndex(
        string id, 
        IPackageMetadataService _metadata, 
        [FromQuery] string semVerLevel,
        CancellationToken cancellationToken)
    {
        // For backward compatibility, default behavior includes all packages (SemVer1 + SemVer2)
        // unless client explicitly sets semVerLevel to exclude SemVer2
        var includeSemVer2 = string.IsNullOrEmpty(semVerLevel) || semVerLevel == "2.0.0";
        var index = await _metadata.GetRegistrationIndexOrNullAsync(id, includeSemVer2, cancellationToken);
        if (index == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(index);
    }

    private static WebApplication MapRegistrationLeaf(this WebApplication app)
    {
        app.MapGet("v3/registration/{id}/{version}.json", GetRegistrationLeaf)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .UseNugetCaching()
           .WithTags(nameof(PackageMetadata))
           .WithName(Routes.RegistrationLeafRouteName);
        return app;
    }

    /// <summary>
    /// Gets the Registration Leaf
    /// </summary>
    /// <param name="id"></param>
    /// <param name="version"></param>
    /// <param name="_metadata"></param>
    /// <param name="semVerLevel">SemVer level (2.0.0 to include SemVer2 packages)</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <response code="200">Returns the Registration Leaf response</response>
    /// <response code="404">No package or version was found.</response>
    [Produces("application/json")]
    [ProducesResponseType(typeof(RegistrationLeafResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404)]
    private static async ValueTask<IResult> GetRegistrationLeaf(
        string id, 
        string version, 
        IPackageMetadataService _metadata,
        [FromQuery] string semVerLevel, 
        CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }

        // For backward compatibility, default behavior includes all packages (SemVer1 + SemVer2)
        // unless client explicitly sets semVerLevel to exclude SemVer2
        var includeSemVer2 = string.IsNullOrEmpty(semVerLevel) || semVerLevel == "2.0.0";
        var leaf = await _metadata.GetRegistrationLeafOrNullAsync(id, nugetVersion, includeSemVer2, cancellationToken);
        if (leaf == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(leaf);
    }

    // RegistrationsBaseUrl/3.4.0 - SemVer1 only, gzipped
    private static WebApplication MapRegistrationIndexGzSemVer1(this WebApplication app)
    {
        app.MapGet("v3/registration-gz-semver1/{id}/index.json", GetRegistrationIndexGzSemVer1)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .AddEndpointFilter<Middleware.GzipCompressionFilter>()
           .UseNugetCaching()
           .WithTags(nameof(PackageMetadata))
           .WithName(Routes.RegistrationIndexGzSemVer1RouteName);
        return app;
    }

    [Produces("application/json")]
    [ProducesResponseType(typeof(NuGetApiRegistrationIndexResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404)]
    private static async ValueTask<IResult> GetRegistrationIndexGzSemVer1(
        string id, 
        IPackageMetadataService _metadata,
        CancellationToken cancellationToken)
    {
        var index = await _metadata.GetRegistrationIndexOrNullAsync(id, includeSemVer2: false, cancellationToken);
        if (index == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(index);
    }

    private static WebApplication MapRegistrationLeafGzSemVer1(this WebApplication app)
    {
        app.MapGet("v3/registration-gz-semver1/{id}/{version}.json", GetRegistrationLeafGzSemVer1)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .AddEndpointFilter<Middleware.GzipCompressionFilter>()
           .UseNugetCaching()
           .WithTags(nameof(PackageMetadata))
           .WithName(Routes.RegistrationLeafGzSemVer1RouteName);
        return app;
    }

    [Produces("application/json")]
    [ProducesResponseType(typeof(RegistrationLeafResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404)]
    private static async ValueTask<IResult> GetRegistrationLeafGzSemVer1(
        string id, 
        string version, 
        IPackageMetadataService _metadata,
        CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }

        var leaf = await _metadata.GetRegistrationLeafOrNullAsync(id, nugetVersion, includeSemVer2: false, cancellationToken);
        if (leaf == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(leaf);
    }

    // RegistrationsBaseUrl/3.6.0 - SemVer2 capable, gzipped
    private static WebApplication MapRegistrationIndexGzSemVer2(this WebApplication app)
    {
        app.MapGet("v3/registration-gz-semver2/{id}/index.json", GetRegistrationIndexGzSemVer2)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .AddEndpointFilter<Middleware.GzipCompressionFilter>()
           .UseNugetCaching()
           .WithTags(nameof(PackageMetadata))
           .WithName(Routes.RegistrationIndexGzSemVer2RouteName);
        return app;
    }

    [Produces("application/json")]
    [ProducesResponseType(typeof(NuGetApiRegistrationIndexResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404)]
    private static async ValueTask<IResult> GetRegistrationIndexGzSemVer2(
        string id, 
        IPackageMetadataService _metadata,
        CancellationToken cancellationToken)
    {
        var index = await _metadata.GetRegistrationIndexOrNullAsync(id, includeSemVer2: true, cancellationToken);
        if (index == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(index);
    }

    private static WebApplication MapRegistrationLeafGzSemVer2(this WebApplication app)
    {
        app.MapGet("v3/registration-gz-semver2/{id}/{version}.json", GetRegistrationLeafGzSemVer2)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .AddEndpointFilter<Middleware.GzipCompressionFilter>()
           .UseNugetCaching()
           .WithTags(nameof(PackageMetadata))
           .WithName(Routes.RegistrationLeafGzSemVer2RouteName);
        return app;
    }

    [Produces("application/json")]
    [ProducesResponseType(typeof(RegistrationLeafResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404)]
    private static async ValueTask<IResult> GetRegistrationLeafGzSemVer2(
        string id, 
        string version, 
        IPackageMetadataService _metadata,
        CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }

        var leaf = await _metadata.GetRegistrationLeafOrNullAsync(id, nugetVersion, includeSemVer2: true, cancellationToken);
        if (leaf == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(leaf);
    }
}
