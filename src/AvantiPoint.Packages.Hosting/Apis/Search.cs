using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AvantiPoint.Packages.Hosting;

internal static class Search
{
    public static WebApplication MapSearchRoutes(this WebApplication app) =>
        app.MapSearch()
           .MapAutocomplete()
           .MapDependents();

    private static WebApplication MapSearch(this WebApplication app)
    {
        app.MapGet("v3/search", GetSearch)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .WithTags(nameof(Search))
           .WithName(Routes.SearchRouteName);
        return app;
    }

    [ProducesResponseType(typeof(SearchResponse), 200, "application/json")]
    private static async ValueTask<IResult> GetSearch(
        ISearchService searchService,
        [FromQuery(Name = "q")] string query = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] bool prerelease = false,
        [FromQuery] string semVerLevel = null,

        // These are unofficial parameters
        [FromQuery] string packageType = null,
        [FromQuery] string framework = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest
        {
            Skip = skip,
            Take = take,
            IncludePrerelease = prerelease,
            IncludeSemVer2 = semVerLevel == "2.0.0",
            PackageType = packageType,
            Framework = framework,
            Query = query ?? string.Empty,
        };

        var result = await searchService.SearchAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static WebApplication MapAutocomplete(this WebApplication app)
    {
        app.MapGet("v3/autocomplete", GetAutocomplete)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .WithTags(nameof(Search))
           .WithName(Routes.AutocompleteRouteName);
        return app;
    }

    [ProducesResponseType(typeof(AutocompleteResponse), 200, "application/json")]
    private static async ValueTask<IResult> GetAutocomplete(
        ISearchService searchService,
        [FromQuery(Name = "q")] string autocompleteQuery = null,
        [FromQuery(Name = "id")] string versionsQuery = null,
        [FromQuery] bool prerelease = false,
        [FromQuery] string semVerLevel = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,

        // These are unofficial parameters
        [FromQuery] string packageType = null,
        CancellationToken cancellationToken = default)
    {
        // If only "id" is provided, find package versions. Otherwise, find package IDs.
        if (versionsQuery != null && autocompleteQuery == null)
        {
            var versionsRequest = new VersionsRequest
            {
                IncludePrerelease = prerelease,
                IncludeSemVer2 = semVerLevel == "2.0.0",
                PackageId = versionsQuery,
            };

            return Results.Ok(await searchService.ListPackageVersionsAsync(versionsRequest, cancellationToken));
        }

        var request = new AutocompleteRequest
        {
            IncludePrerelease = prerelease,
            IncludeSemVer2 = semVerLevel == "2.0.0",
            PackageType = packageType,
            Skip = skip,
            Take = take,
            Query = autocompleteQuery,
        };

        return Results.Ok(await searchService.AutocompleteAsync(request, cancellationToken));
    }

    private static WebApplication MapDependents(this WebApplication app)
    {
        app.MapGet("v3/dependents", GetDependents)
           .AllowAnonymous()
           .AddEndpointFilter<AuthorizedNuGetConsumerFilter>()
           .WithTags(nameof(Search))
           .WithName(Routes.DependentsRouteName);
        return app;
    }

    [ProducesResponseType(typeof(DependentsResponse), 200, "application/json")]
    [ProducesResponseType(typeof(BadRequestResult), 400, "application/json")]
    private static async ValueTask<IResult> GetDependents(
        ISearchService searchService,
        [FromQuery] string packageId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return Results.BadRequest();
        }

        var result = await searchService.FindDependentsAsync(packageId, cancellationToken);
        return Results.Ok(result);
    }
}
