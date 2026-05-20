using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Registry.Npm.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Npm;

public static class NpmRegistryEndpoints
{
    public static IServiceCollection AddNpmRegistry(this IServiceCollection services)
    {
        services.AddScoped<INpmPackageService, NpmPackageService>();
        return services;
    }

    public static RouteGroupBuilder MapNpmRegistryRoutes(this WebApplication app, string routePrefix = "/npm")
    {
        var prefix = routePrefix.TrimEnd('/');
        var group = app.MapGroup(prefix);

        group.MapGet("/-/whoami", WhoAmI)
            .AddEndpointFilter<AuthorizedNpmConsumerFilter>();

        group.MapGet("/-/v1/search", Search)
            .AddEndpointFilter<AuthorizedNpmConsumerFilter>();

        group.MapGet("/{*packagePath}", GetPackage)
            .AddEndpointFilter<AuthorizedNpmConsumerFilter>();

        group.MapPut("/{*packagePath}", Publish)
            .AddEndpointFilter<AuthorizedNpmPublisherFilter>();

        group.MapPut("/-/user/{*path}", LoginStub);

        return group;
    }

    private static IResult WhoAmI(ISurfaceContextAccessor surfaceAccessor)
    {
        var surface = surfaceAccessor.Current;
        return Results.Json(new { username = surface?.SurfaceId ?? "npm" });
    }

    private static IResult Search() =>
        Results.Json(new { objects = Array.Empty<object>() });

    private static IResult LoginStub() =>
        Results.Json(new
        {
            ok = true,
            token = "npm-token",
        });

    private static Task<IResult> GetPackage(
        string packagePath,
        INpmPackageService service,
        ISurfaceContextAccessor surfaceAccessor,
        IFeedActionHandler? actionHandler,
        CancellationToken cancellationToken)
    {
        if (IsInternalPath(packagePath))
        {
            return Task.FromResult<IResult>(Results.NotFound());
        }

        if (TryParseTarballPath(packagePath, out var packageName, out var tarballFileName))
        {
            return GetTarball(packageName, tarballFileName, service, surfaceAccessor, actionHandler, cancellationToken);
        }

        return GetPackument(packagePath, service, surfaceAccessor, cancellationToken);
    }

    private static async Task<IResult> GetPackument(
        string packagePath,
        INpmPackageService service,
        ISurfaceContextAccessor surfaceAccessor,
        CancellationToken cancellationToken)
    {
        var surface = surfaceAccessor.Current!;
        var packument = await service.GetPackumentAsync(
            surface.FeedId,
            packagePath,
            surface.PublicBaseUrl,
            cancellationToken);

        return packument is null ? Results.NotFound() : Results.Json(packument);
    }

    private static async Task<IResult> GetTarball(
        string packagePath,
        string tarball,
        INpmPackageService service,
        ISurfaceContextAccessor surfaceAccessor,
        IFeedActionHandler? actionHandler,
        CancellationToken cancellationToken)
    {
        var surface = surfaceAccessor.Current!;
        var stream = await service.GetTarballAsync(
            surface.FeedId,
            packagePath,
            tarball,
            cancellationToken);

        if (stream is null)
        {
            return Results.NotFound();
        }

        if (actionHandler is not null)
        {
            var context = new FeedArtifactEventContext(
                surface,
                NpmPackageService.NormalizePackageName(packagePath),
                ExtractVersionFromTarball(tarball),
                tarball);

            if (await actionHandler.CanAccessArtifact(context, cancellationToken))
            {
                await actionHandler.OnArtifactDownloaded(context, cancellationToken);
            }
        }

        return Results.Stream(stream, "application/octet-stream");
    }

    private static async Task<IResult> Publish(
        string packagePath,
        HttpRequest request,
        INpmPackageService service,
        ISurfaceContextAccessor surfaceAccessor,
        IFeedActionHandler? actionHandler,
        CancellationToken cancellationToken)
    {
        if (IsInternalPath(packagePath))
        {
            return Results.BadRequest();
        }

        if (!request.Body.CanRead)
        {
            return Results.BadRequest(new { error = "Missing package body." });
        }

        var surface = surfaceAccessor.Current!;
        var packageName = NpmPackageService.NormalizePackageName(packagePath);

        await using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, cancellationToken);
        body.Position = 0;

        var parsed = await TryParsePublishBodyAsync(body, cancellationToken);
        if (parsed is null)
        {
            return Results.BadRequest(new { error = "Invalid publish payload." });
        }

        var (metadata, tarballStream) = parsed.Value;
        var version = metadata["version"]?.GetValue<string>();
        if (string.IsNullOrEmpty(version))
        {
            return Results.BadRequest(new { error = "Version is required." });
        }

        await using (tarballStream)
        {
            await service.PublishAsync(
                surface.FeedId,
                packageName,
                version,
                tarballStream,
                metadata,
                surface.PublicBaseUrl,
                cancellationToken);
        }

        if (actionHandler is not null)
        {
            var eventContext = new FeedArtifactEventContext(surface, packageName, version, null);
            await actionHandler.OnArtifactUploaded(eventContext, cancellationToken);
        }

        return Results.Created($"{surface.PublicBaseUrl}{packagePath}", new { ok = true });
    }

    private static async Task<(JsonObject Metadata, Stream Tarball)?> TryParsePublishBodyAsync(
        Stream body,
        CancellationToken cancellationToken)
    {
        body.Position = 0;
        try
        {
            var node = await JsonNode.ParseAsync(body, cancellationToken: cancellationToken);
            if (node is not JsonObject root)
            {
                return null;
            }

            var attachments = GetAttachmentsObject(root);
            if (attachments is { Count: > 0 })
            {
                var first = attachments.First();
                var attachment = first.Value as JsonObject;
                var data = attachment?["data"]?.GetValue<string>();
                if (string.IsNullOrEmpty(data))
                {
                    return null;
                }

                var bytes = Convert.FromBase64String(data);
                var tarball = new MemoryStream(bytes);

                var versions = root["versions"] as JsonObject;
                var versionKey = versions?.First().Key ?? "0.0.0";
                var metadata = versions?[versionKey] as JsonObject ?? new JsonObject();
                if (metadata["version"] is null)
                {
                    metadata["version"] = versionKey;
                }

                if (metadata["name"] is null)
                {
                    metadata["name"] = root["name"]?.GetValue<string>() ?? "package";
                }

                return (metadata, tarball);
            }

            if (root["version"] is not null)
            {
                body.Position = 0;
                return (root, body);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static JsonObject? GetAttachmentsObject(JsonObject root) =>
        root["attachments"] as JsonObject ?? root["_attachments"] as JsonObject;

    private static bool IsInternalPath(string path) =>
        path.StartsWith("-/", StringComparison.Ordinal);

    private static bool TryParseTarballPath(string packagePath, out string packageName, out string tarballFileName)
    {
        packageName = string.Empty;
        tarballFileName = string.Empty;

        var separator = packagePath.LastIndexOf("/-/", StringComparison.Ordinal);
        if (separator < 0)
        {
            return false;
        }

        packageName = packagePath[..separator];
        tarballFileName = packagePath[(separator + 3)..];
        return !string.IsNullOrEmpty(packageName) && !string.IsNullOrEmpty(tarballFileName);
    }

    private static string? ExtractVersionFromTarball(string tarball)
    {
        var name = Path.GetFileNameWithoutExtension(tarball);
        var dash = name.LastIndexOf('-');
        return dash > 0 ? name[(dash + 1)..] : null;
    }
}
