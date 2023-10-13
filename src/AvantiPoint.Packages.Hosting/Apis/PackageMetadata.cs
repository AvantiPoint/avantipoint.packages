using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Hosting;

internal static class PackageMetadata
{
    public static WebApplication MapPackageMetadataRoutes(this WebApplication app) =>
        app.MapRegistrationIndex()
           .MapRegistrationLeaf();

    private static WebApplication MapRegistrationIndex(this WebApplication app)
    {
        app.MapGet("v3/registration/{id}/index.json", GetRegistrationIndex)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .WithTags(nameof(PackageMetadata))
           .WithName(Routes.RegistrationIndexRouteName);
        return app;
    }

    /// <summary>
    /// Get's the Registration Index for a specified Package Id
    /// </summary>
    /// <param name="id">The Package Id</param>
    /// <param name="_metadata"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <response code="200">Returns the Api Registration Index response</response>
    /// <response code="404">No package was found.</response>
    [Produces("application/json")]
    [ProducesResponseType(typeof(NuGetApiRegistrationIndexResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404)]
    private static async ValueTask<IResult> GetRegistrationIndex(string id, IPackageMetadataService _metadata, CancellationToken cancellationToken)
    {
        var index = await _metadata.GetRegistrationIndexOrNullAsync(id, cancellationToken);
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
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <response code="200">Returns the Registration Leaf response</response>
    /// <response code="404">No package or version was found.</response>
    [Produces("application/json")]
    [ProducesResponseType(typeof(RegistrationLeafResponse), 200)]
    [ProducesResponseType(typeof(StatusCodeResult), 404)]
    private static async ValueTask<IResult> GetRegistrationLeaf(string id, string version, IPackageMetadataService _metadata, CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return Results.NotFound();
        }

        var leaf = await _metadata.GetRegistrationLeafOrNullAsync(id, nugetVersion, cancellationToken);
        if (leaf == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(leaf);
    }
}
