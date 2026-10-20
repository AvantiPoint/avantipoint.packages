using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Registry.Oci.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;

namespace AvantiPoint.Packages.Registry.Oci;

public static class OciRegistryEndpoints
{
    private const string DistributionHeader = "Docker-Distribution-API-Version";
    private const string DistributionVersion = "registry/2.0";

    public static IServiceCollection AddOciRegistry(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(OciMirrorService));
        // Hosts may pre-register a database-backed provider; configuration is the default.
        services.TryAddScoped<IOciUpstreamRegistryProvider, ConfigurationOciUpstreamRegistryProvider>();
        services.AddScoped<IOciMirrorService, OciMirrorService>();
        services.AddScoped<IOciRegistryService, OciRegistryService>();
        services.AddSingleton<OciFeedOptionsAccessor>();
        services.AddScoped<OciGarbageCollectionService>();
        services.AddScoped<OciUploadCleanupService>();
        services.AddOptions<OciUploadCleanupOptions>().BindConfiguration("Feed:Oci:UploadCleanup")
            .Validate(options => options.Interval >= TimeSpan.FromSeconds(10) && options.Interval <= TimeSpan.FromHours(1), "OCI upload cleanup interval must be between ten seconds and one hour.")
            .Validate(options => options.IdleTimeout >= TimeSpan.FromMinutes(1) && options.IdleTimeout <= TimeSpan.FromDays(30), "OCI upload timeout must be between one minute and thirty days.")
            .ValidateOnStart();
        services.AddHostedService<OciUploadCleanupHostedService>();
        services.AddOptions<OciGarbageCollectionOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OciGarbageCollectionHostedService>();
        services.AddHostedService<OciStorageMetricsInitializer>();
        return services;
    }

    public static RouteGroupBuilder MapOciRegistryRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v2");
        group.MapMethods("/", ["GET"], GetVersion).AddEndpointFilter<AuthorizedOciProbeFilter>();
        group.MapMethods("/_catalog", ["GET"], GetCatalog).AddEndpointFilter<AuthorizedOciPullFilter>();
        group.Map("/{**path}", async (
            HttpContext httpContext,
            string path,
            IOciRegistryService service,
            IFeedAuthenticationService authentication,
            ISurfaceContextAccessor surfaceAccessor,
            IFeedActionHandler? actionHandler,
            CancellationToken cancellationToken) =>
        {
            var result = await DispatchRequest(
                httpContext,
                path,
                service,
                authentication,
                surfaceAccessor,
                actionHandler,
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
        if (!TryParsePageSize(request, out var max)) return InvalidPageSize();
        var last = request.Query["last"].FirstOrDefault();
        var catalog = await service.ListCatalogAsync(surface, max, last, cancellationToken);
        if (catalog.HasMore && catalog.Repositories.Count > 0)
            SetNextPage(response, surface, "_catalog", max, catalog.Repositories[^1]);
        return Results.Json(new { repositories = catalog.Repositories });
    }

    private static async Task<IResult> DispatchRequest(
        HttpContext httpContext,
        string path,
        IOciRegistryService service,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor,
        IFeedActionHandler? actionHandler,
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

        var method = httpContext.Request.Method;
        var operation = IsWriteOperation(method, route)
            ? FeedOperation.Push
            : FeedOperation.Pull;
        var authResult = await authentication.AuthenticateAsync(
            new FeedAuthenticationRequest(surface, httpContext, operation),
            cancellationToken);

        // Docker, Helm, and other OCI clients issue HEAD requests while preparing a
        // push to avoid uploading content the registry already has. These checks do
        // not return artifact content, so allow a publish-only token to perform them
        // after normal pull authorization has failed.
        if (!authResult.Succeeded && IsExistenceCheck(method, route))
        {
            authResult = await authentication.AuthenticateAsync(
                new FeedAuthenticationRequest(surface, httpContext, FeedOperation.Push),
                cancellationToken);
        }

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

        if (operation == FeedOperation.Push && route.Kind is OciRouteKind.UploadSession or OciRouteKind.StartUpload)
        {
            var limit = httpContext.RequestServices.GetRequiredService<OciFeedOptionsAccessor>()
                .GetOptions(surface).MaxUploadRequestBytes;
            if (limit <= 0) throw new InvalidOperationException("OCI MaxUploadRequestBytes must be positive.");
            if (httpContext.Request.ContentLength > limit)
                return Results.Json(new { errors = new[] { new { code = "SIZE_INVALID", message = "Upload request exceeds the configured OCI size limit." } } }, statusCode: 413);
            var bodyLimit = httpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = limit;
        }

        if (route.Kind is OciRouteKind.Blob or OciRouteKind.Referrers && !IsValidDigest(route.Digest))
            return Results.BadRequest(new { errors = new[] { new { code = "DIGEST_INVALID", message = "The digest is invalid." } } });

        if (route.Kind == OciRouteKind.Manifest && method == HttpMethods.Get)
        {
            return await HandleGetManifest(
                httpContext.Response,
                service,
                surface,
                route,
                actionHandler,
                cancellationToken);
        }

        if (route.Kind == OciRouteKind.Manifest && method == HttpMethods.Head)
        {
            return await HandleHeadManifest(httpContext.Response, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.Manifest && method == HttpMethods.Put)
        {
            return await HandlePutManifest(httpContext, service, surface, route, actionHandler, cancellationToken);
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

        if (route.Kind == OciRouteKind.UploadSession && (method == HttpMethods.Get || method == HttpMethods.Delete))
        {
            try
            {
                if (method == HttpMethods.Delete)
                    await service.CancelUploadAsync(surface, route.RepositoryName!, route.UploadId!, cancellationToken);
                else
                {
                    var status = await service.GetUploadStatusAsync(surface, route.RepositoryName!, route.UploadId!, cancellationToken);
                    httpContext.Response.Headers.Location = status.Location;
                    httpContext.Response.Headers["Range"] = $"0-{status.RangeEnd}";
                    httpContext.Response.Headers["Docker-Upload-UUID"] = status.UploadId;
                }
                return Results.NoContent();
            }
            catch (OciRegistryException exception)
            {
                return UploadError(httpContext.Response, exception);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { errors = new[] { new { code = "BLOB_UPLOAD_INVALID", message = "The upload changed concurrently. Query its status before retrying." } } });
            }
            catch (NotSupportedException)
            {
                return Results.Json(new { errors = new[] { new { code = "UNSUPPORTED", message = "This upload operation is not supported." } } }, statusCode: StatusCodes.Status405MethodNotAllowed);
            }
        }

        if (route.Kind == OciRouteKind.ListTags && method == HttpMethods.Get)
        {
            return await HandleListTags(httpContext.Request, httpContext.Response, service, surface, route, cancellationToken);
        }

        if (route.Kind == OciRouteKind.Referrers && method == HttpMethods.Get)
        {
            return await HandleListReferrers(
                httpContext.Request,
                httpContext.Response,
                service,
                surface,
                route,
                cancellationToken);
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

    private static bool IsExistenceCheck(string method, OciRoute route) =>
        method == HttpMethods.Head
        && route.Kind is OciRouteKind.Manifest or OciRouteKind.Blob;

    private static async Task<IResult> HandleGetManifest(
        HttpResponse response,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        IFeedActionHandler? actionHandler,
        CancellationToken cancellationToken)
    {
        var manifest = await service.GetManifestAsync(surface, route.RepositoryName!, route.Reference!, cancellationToken);
        if (manifest is null)
        {
            return Results.NotFound();
        }

        if (actionHandler is not null && !route.Reference!.Contains(':', StringComparison.Ordinal))
        {
            var eventContext = new FeedArtifactEventContext(
                surface,
                route.RepositoryName!,
                route.Reference,
                manifest.Digest);
            await actionHandler.OnArtifactDownloaded(eventContext, cancellationToken);
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
        response.Headers["Content-Length"] = manifest.Content.Length.ToString();
        return Results.StatusCode(StatusCodes.Status200OK);
    }

    private static async Task<IResult> HandlePutManifest(
        HttpContext httpContext,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        IFeedActionHandler? actionHandler,
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

            if (actionHandler is not null && !route.Reference!.Contains(':', StringComparison.Ordinal))
            {
                var eventContext = new FeedArtifactEventContext(
                    surface,
                    route.RepositoryName!,
                    route.Reference,
                    result.Digest);
                await actionHandler.OnArtifactUploaded(eventContext, cancellationToken);
            }

            ApplyManifestHeaders(httpContext.Response, result.Digest, result.MediaType);
            if (!string.IsNullOrWhiteSpace(result.SubjectDigest))
            {
                httpContext.Response.Headers["OCI-Subject"] = result.SubjectDigest;
            }
            var baseUrl = surface.PublicBaseUrl.ToString().TrimEnd('/');
            return Results.Created(
                $"{baseUrl}/v2/{route.RepositoryName}/manifests/{route.Reference}",
                null);
        }
        catch (OciRegistryException ex)
        {
            var code = ex.Code == "BLOB_UPLOAD_INVALID" ? "MANIFEST_INVALID" : ex.Code;
            return Results.Json(new { errors = new[] { new { code, message = ex.Message } } }, statusCode: ex.StatusCode);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { errors = new[] { new { code = "MANIFEST_INVALID", message = ex.Message } } });
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { errors = new[] { new { code = "MANIFEST_INVALID", message = "The manifest contains an invalid digest." } } });
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
            var exists = await service.BlobExistsAsync(surface, route.Digest!, route.RepositoryName, cancellationToken);
            if (!exists.Exists)
            {
                return Results.NotFound();
            }

            httpContext.Response.Headers["Content-Length"] = exists.Size.ToString();
            httpContext.Response.Headers["Docker-Content-Digest"] = route.Digest;
            return Results.StatusCode(StatusCodes.Status200OK);
        }

        var blob = await service.GetBlobAsync(surface, route.Digest!, route.RepositoryName, cancellationToken);
        if (blob is null)
        {
            return Results.NotFound();
        }

        return new OciBlobStreamResult(blob.Content, blob.Digest, blob.Size);
    }

    private sealed class OciBlobStreamResult(Stream content, string digest, long? size) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.ContentType = "application/octet-stream";
            response.Headers["Docker-Content-Digest"] = digest;
            response.ContentLength = size;

            try
            {
                await content.CopyToAsync(response.Body, httpContext.RequestAborted);
            }
            finally
            {
                await content.DisposeAsync();
            }
        }
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
        response.Headers["Docker-Upload-UUID"] = result.UploadId;
        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> HandlePatchUpload(
        HttpContext httpContext,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        if (!ParseContentRange(httpContext.Request.Headers.ContentRange.ToString(), out var start, out var end))
            return Results.BadRequest(new { errors = new[] { new { code = "BLOB_UPLOAD_INVALID", message = "Invalid Content-Range." } } });

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
            httpContext.Response.Headers["Docker-Upload-UUID"] = result.UploadId;
            return Results.StatusCode(StatusCodes.Status202Accepted);
        }
        catch (OciRegistryException ex)
        {
            return UploadError(httpContext.Response, ex);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { errors = new[] { new { code = "BLOB_UPLOAD_INVALID", message = "The upload changed concurrently. Query its status before retrying." } } });
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
        if (!ParseContentRange(httpContext.Request.Headers.ContentRange.ToString(), out var start, out var end))
            return Results.BadRequest(new { errors = new[] { new { code = "BLOB_UPLOAD_INVALID", message = "Invalid Content-Range." } } });

        try
        {
            var result = await service.CompleteRangedUploadAsync(
                surface,
                route.RepositoryName!,
                route.UploadId!,
                digest,
                httpContext.Request.Body,
                start,
                end,
                cancellationToken);

            httpContext.Response.Headers["Location"] = result.Location;
            httpContext.Response.Headers["Docker-Content-Digest"] = result.Digest;
            return Results.Created(result.Location, null);
        }
        catch (OciRegistryException ex)
        {
            return UploadError(httpContext.Response, ex);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { errors = new[] { new { code = "DIGEST_INVALID", message = "The upload digest is invalid." } } });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { errors = new[] { new { code = "BLOB_UPLOAD_INVALID", message = "The upload changed concurrently. Query its status before retrying." } } });
        }
        catch (NotSupportedException)
        {
            return Results.Json(new { errors = new[] { new { code = "UNSUPPORTED", message = "Ranged upload completion is not supported." } } }, statusCode: StatusCodes.Status405MethodNotAllowed);
        }
    }

    private static async Task<IResult> HandleListTags(
        HttpRequest request,
        HttpResponse response,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        if (!TryParsePageSize(request, out var max)) return InvalidPageSize();
        var last = request.Query["last"].FirstOrDefault();
        var tags = await service.ListTagsAsync(surface, route.RepositoryName!, max, last, cancellationToken);
        if (tags is { HasMore: true, Tags.Count: > 0 })
            SetNextPage(response, surface, $"{route.RepositoryName}/tags/list", max, tags.Tags[^1]);
        return tags is null
            ? Results.NotFound()
            : Results.Json(new { name = tags.Name, tags = tags.Tags });
    }

    private static async Task<IResult> HandleListReferrers(
        HttpRequest request,
        HttpResponse response,
        IOciRegistryService service,
        SurfaceContext surface,
        OciRoute route,
        CancellationToken cancellationToken)
    {
        if (!IsValidDigest(route.Digest))
        {
            return Results.BadRequest(new
            {
                errors = new[]
                {
                    new { code = "DIGEST_INVALID", message = "The subject digest is invalid." },
                },
            });
        }

        var artifactType = request.Query["artifactType"].FirstOrDefault();
        var referrers = await service.ListReferrersAsync(
            surface,
            route.RepositoryName!,
            route.Digest!,
            artifactType,
            cancellationToken);
        if (!referrers.RepositoryFound)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(artifactType))
        {
            response.Headers["OCI-Filters-Applied"] = "artifactType";
        }

        return Results.Bytes(
            OciReferrersIndex.Serialize(referrers.Manifests),
            OciReferrersIndex.MediaType);
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

    private static bool TryParsePageSize(HttpRequest request, out int? size)
    {
        size = null;
        if (!request.Query.TryGetValue("n", out var values)) return true;
        if (values.Count != 1 || !int.TryParse(values[0], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return false;
        size = OciRegistryService.PageSize(parsed);
        return true;
    }

    private static IResult InvalidPageSize() => Results.BadRequest(new
    {
        errors = new[] { new { code = "PAGINATION_NUMBER_INVALID", message = "n must be a nonnegative integer." } },
    });

    private static void SetNextPage(HttpResponse response, SurfaceContext surface, string suffix, int? size, string last)
    {
        var url = $"{surface.PublicBaseUrl.ToString().TrimEnd('/')}/v2/{suffix}?n={OciRegistryService.PageSize(size)}&last={Uri.EscapeDataString(last)}";
        response.Headers.Link = $"<{url}>; rel=\"next\"";
    }

    private static bool IsValidDigest(string? digest)
    {
        try { AvantiPoint.Feed.Platform.Storage.DigestBlobStore.ParseDigest(digest!); return true; }
        catch (ArgumentException) { return false; }
    }

    private static IResult UploadError(HttpResponse response, OciRegistryException exception)
    {
        if (exception.RangeEnd is { } rangeEnd) response.Headers["Range"] = $"0-{rangeEnd}";
        return Results.Json(new { errors = new[] { new { code = exception.Code, message = exception.Message } } }, statusCode: exception.StatusCode);
    }

    private static bool ParseContentRange(string? header, out long? start, out long? end)
    {
        start = null;
        end = null;
        if (string.IsNullOrEmpty(header))
        {
            return true;
        }

        var range = (header.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase) ? header["bytes ".Length..] : header).Split('/', 2)[0];
        var parts = range.Split('-', 2);
        if (parts.Length == 2 && long.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsedStart)
            && long.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsedEnd) && parsedEnd >= parsedStart)
        {
            start = parsedStart;
            end = parsedEnd;
            return true;
        }
        return false;
    }
}
