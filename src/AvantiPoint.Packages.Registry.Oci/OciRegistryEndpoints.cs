using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Registry.Oci.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AvantiPoint.Packages.Registry.Oci;

public static class OciRegistryEndpoints
{
    private const string DistributionHeader = "Docker-Distribution-API-Version";
    private const string DistributionVersion = "registry/2.0";

    public static IServiceCollection AddOciRegistry(this IServiceCollection services)
    {
        services.AddScoped<IOciRegistryService, OciRegistryService>();
        services.AddSingleton<OciFeedOptionsAccessor>();
        services.AddScoped<OciGarbageCollectionService>();
        return services;
    }

    public static RouteGroupBuilder MapOciRegistryRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v2");
        group.MapMethods("/", ["GET"], GetVersion).AddEndpointFilter<AuthorizedOciPullFilter>();
        group.MapMethods("/_catalog", ["GET"], GetCatalog).AddEndpointFilter<AuthorizedOciPullFilter>();
        group.Map("/{**path}", async (
            HttpContext httpContext,
            string path,
            IOciRegistryService service,
            IFeedAuthenticationService authentication,
            ISurfaceContextAccessor surfaceAccessor,
            CancellationToken cancellationToken) =>
        {
            var result = await DispatchRequest(
                httpContext,
                path,
                service,
                authentication,
                surfaceAccessor,
                cancellationToken);
            await result.ExecuteAsync(httpContext);
        });
        return group;
    }

    private static IResult GetVersion(HttpResponse response)
    {
        response.Headers[DistributionHeader] = DistributionVersion;
        return Results.Content("{}", "application/json");
    }

    private static async Task<IResult> GetCatalog(
        HttpRequest request,
        HttpResponse response,
        IOciRegistryService service,
        ISurfaceContextAccessor surfaceAccessor,
        CancellationToken cancellationToken)
    {
        response.Headers[DistributionHeader] = DistributionVersion;
        var surface = surfaceAccessor.Current!;
        var max = ParseIntQuery(request.Query["n"]);
        var last = request.Query["last"].FirstOrDefault();
        var catalog = await service.ListCatalogAsync(surface, max, last, cancellationToken);
        return Results.Json(new { repositories = catalog.Repositories });
    }

    private static async Task<IResult> DispatchRequest(
        HttpContext httpContext,
        string path,
        IOciRegistryService service,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor,
        CancellationToken cancellationToken)
    {
        if (!OciRouteParser.TryParse(new PathString("/v2/" + path), out var route))
        {
            return Results.NotFound();
        }

        var surface = surfaceAccessor.Current;
        if (surface is null || surface.Protocol != FeedProtocol.Oci)
        {
            return Results.NotFound();
        }

        var isWrite = IsWriteOperation(httpContext.Request.Method, route);
        var authResult = await authentication.AuthenticateAsync(
            new FeedAuthenticationRequest(
                surface,
                httpContext,
                isWrite ? FeedOperation.Push : FeedOperation.Pull),
            cancellationToken);

        if (!authResult.Succeeded)
        {
            ApplyFailureHeaders(httpContext.Response, authResult);
            return Results.Unauthorized();
        }

        if (authResult.User is not null)
        {
            httpContext.User = authResult.User;
        }

        httpContext.Response.Headers[DistributionHeader] = DistributionVersion;

        var method = httpContext.Request.Method;
        if (route.Kind == OciRouteKind.Manifest && method == HttpMethods.Get)
        {
            return await HandleGetManifest(httpContext.Response, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.Manifest && method == HttpMethods.Head)
        {
            return await HandleHeadManifest(httpContext.Response, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.Manifest && method == HttpMethods.Put)
        {
            return await HandlePutManifest(httpContext, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.Blob && (method == HttpMethods.Get || method == HttpMethods.Head))
        {
            return await HandleGetBlob(httpContext, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.StartUpload && method == HttpMethods.Post)
        {
            return await HandleStartUpload(httpContext.Response, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.UploadSession && method == HttpMethods.Patch)
        {
            return await HandlePatchUpload(httpContext, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.UploadSession && method == HttpMethods.Put)
        {
            return await HandleCompleteUpload(httpContext, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.ListTags && method == HttpMethods.Get)
        {
            return await HandleListTags(httpContext.Request, service, surface, route, cancellationToken);
        }

        return Results.NotFound();
    }

    private static bool IsWriteOperation(string method, OciRoute route) =>
        route.Kind switch
        {
            OciRouteKind.Manifest => method == HttpMethods.Put,
            OciRouteKind.StartUpload or OciRouteKind.UploadSession => true,
            _ => false,
        };

    private static async Task<IResult> HandleGetManifest(
        HttpResponse response,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        var manifest = await service.GetManifestAsync(surface, route.RepositoryName!, route.Reference!, cancellationToken);
        if (manifest is null)
        {
            return Results.NotFound();
        }

        ApplyManifestHeaders(response, manifest.Digest, manifest.MediaType);
        return Results.Bytes(manifest.Content, manifest.MediaType);
    }

    private static async Task<IResult> HandleHeadManifest(
        HttpResponse response,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        var manifest = await service.GetManifestAsync(surface, route.RepositoryName!, route.Reference!, cancellationToken);
        if (manifest is null)
        {
            return Results.NotFound();
        }

        ApplyManifestHeaders(response, manifest.Digest, manifest.MediaType);
        return Results.StatusCode(StatusCodes.Status200OK);
    }

    private static async Task<IResult> HandlePutManifest(
        HttpContext httpContext,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        var mediaType = httpContext.Request.ContentType ?? "application/vnd.oci.image.manifest.v1+json";

        try
        {
            var result = await service.PutManifestAsync(
                surface,
                route.RepositoryName!,
                route.Reference!,
                mediaType,
                httpContext.Request.Body,
                cancellationToken);

            ApplyManifestHeaders(httpContext.Response, result.Digest, result.MediaType);
            return Results.Created(
                $"{surface.PublicBaseUrl}/v2/{route.RepositoryName}/manifests/{route.Reference}",
                null);
        }
        catch (OciRegistryException ex)
        {
            return Results.BadRequest(new { errors = new[] { new { code = "MANIFEST_INVALID", message = ex.Message } } });
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { errors = new[] { new { code = "MANIFEST_INVALID", message = ex.Message } } });
        }
    }

    private static async Task<IResult> HandleGetBlob(
        HttpContext httpContext,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.Method == HttpMethods.Head)
        {
            var exists = await service.BlobExistsAsync(surface, route.Digest!, cancellationToken);
            if (!exists.Exists)
            {
                return Results.NotFound();
            }

            httpContext.Response.Headers["Content-Length"] = exists.Size.ToString();
            httpContext.Response.Headers["Docker-Content-Digest"] = route.Digest;
            return Results.StatusCode(StatusCodes.Status200OK);
        }

        var blob = await service.GetBlobAsync(surface, route.Digest!, cancellationToken);
        if (blob is null)
        {
            return Results.NotFound();
        }

        httpContext.Response.Headers["Docker-Content-Digest"] = blob.Digest;
        return Results.Stream(blob.Content, "application/octet-stream");
    }

    private static async Task<IResult> HandleStartUpload(
        HttpResponse response,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        var result = await service.StartUploadAsync(surface, route.RepositoryName!, cancellationToken);
        response.Headers["Location"] = result.Location;
        response.Headers["Range"] = "0-0";
        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> HandlePatchUpload(
        HttpContext httpContext,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        ParseContentRange(httpContext.Request.Headers.ContentRange.ToString(), out var start, out var end);

        try
        {
            var result = await service.PatchUploadAsync(
                surface,
                route.RepositoryName!,
                route.UploadId!,
                httpContext.Request.Body,
                start,
                end,
                cancellationToken);

            httpContext.Response.Headers["Location"] = result.Location;
            httpContext.Response.Headers["Range"] = $"0-{result.RangeEnd}";
            return Results.StatusCode(StatusCodes.Status202Accepted);
        }
        catch (OciRegistryException ex)
        {
            return Results.NotFound(new { errors = new[] { new { code = "BLOB_UPLOAD_UNKNOWN", message = ex.Message } } });
        }
    }

    private static async Task<IResult> HandleCompleteUpload(
        HttpContext httpContext,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        var digest = httpContext.Request.Query["digest"].FirstOrDefault();
        if (string.IsNullOrEmpty(digest))
        {
            return Results.BadRequest(new { errors = new[] { new { code = "DIGEST_INVALID", message = "Missing digest query parameter." } } });
        }

        try
        {
            var result = await service.CompleteUploadAsync(
                surface,
                route.RepositoryName!,
                route.UploadId!,
                digest,
                httpContext.Request.ContentLength is > 0 ? httpContext.Request.Body : null,
                cancellationToken);

            httpContext.Response.Headers["Location"] = result.Location;
            httpContext.Response.Headers["Docker-Content-Digest"] = result.Digest;
            return Results.Created(result.Location, null);
        }
        catch (OciRegistryException ex)
        {
            return Results.BadRequest(new { errors = new[] { new { code = "BLOB_UPLOAD_INVALID", message = ex.Message } } });
        }
    }

    private static async Task<IResult> HandleListTags(
        HttpRequest request,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        var max = ParseIntQuery(request.Query["n"]);
        var last = request.Query["last"].FirstOrDefault();
        var tags = await service.ListTagsAsync(surface, route.RepositoryName!, max, last, cancellationToken);
        return tags is null
            ? Results.NotFound()
            : Results.Json(new { name = tags.Name, tags = tags.Tags });
    }

    private static void ApplyManifestHeaders(HttpResponse response, string digest, string mediaType)
    {
        response.Headers["Docker-Content-Digest"] = digest;
        response.Headers["Content-Type"] = mediaType;
    }

    private static void ApplyFailureHeaders(HttpResponse response, FeedAuthenticationResult result)
    {
        if (result.ResponseHeaders is null)
        {
            return;
        }

        foreach (var (key, value) in result.ResponseHeaders)
        {
            response.Headers[key] = value;
        }
    }

    private static int? ParseIntQuery(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private static void ParseContentRange(string? header, out long? start, out long? end)
    {
        start = null;
        end = null;
        if (string.IsNullOrEmpty(header) || !header.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var range = header["bytes ".Length..].Split('/', 2)[0];
        var parts = range.Split('-', 2);
        if (parts.Length == 2 && long.TryParse(parts[0], out var parsedStart))
        {
            start = parsedStart;
        }

        if (parts.Length == 2 && long.TryParse(parts[1], out var parsedEnd))
        {
            end = parsedEnd;
        }
    }
}
