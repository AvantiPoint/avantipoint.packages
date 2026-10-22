using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Npm.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Npm;

public static partial class NpmRegistryEndpoints
{
    private const long MaxLoginBodyBytes = 64 * 1024;

    public static IServiceCollection AddNpmRegistry(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(NpmMirrorService));
        // Hosts may pre-register a database-backed provider; configuration is the default.
        services.TryAddScoped<INpmUpstreamRegistryProvider, ConfigurationNpmUpstreamRegistryProvider>();
        services.AddScoped<INpmMirrorService, NpmMirrorService>();
        services.AddScoped<NpmPackageService>();
        services.AddScoped<INpmPackageService>(provider => provider.GetRequiredService<NpmPackageService>());
        services.AddHostedService<NpmArtifactCleanupService>();
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

        group.MapPut("/-/user/{*path}", Login);

        group.MapGet("/-/package/{**tagPath}", GetDistTags).AddEndpointFilter<AuthorizedNpmConsumerFilter>();
        group.MapPut("/-/package/{**tagPath}", SetDistTag).AddEndpointFilter<AuthorizedNpmPublisherFilter>();
        group.MapDelete("/-/package/{**tagPath}", RemoveDistTag).AddEndpointFilter<AuthorizedNpmPublisherFilter>();

        group.MapGet("/{*packagePath}", GetPackage)
            .AddEndpointFilter<AuthorizedNpmConsumerFilter>();
        group.MapPut("/{*packagePath}", Publish)
            .AddEndpointFilter<AuthorizedNpmPublisherFilter>();
        group.MapDelete("/{*packagePath}", Unpublish)
            .AddEndpointFilter<AuthorizedNpmPublisherFilter>();

        return group;
    }

    private static IResult WhoAmI(ISurfaceContextAccessor surfaceAccessor)
    {
        var surface = surfaceAccessor.Current;
        return Results.Json(new { username = surface?.SurfaceId ?? "npm" });
    }

    private static async Task<IResult> Search(
        HttpRequest request,
        INpmPackageService service,
        ISurfaceContextAccessor surfaceAccessor,
        CancellationToken cancellationToken)
    {
        var query = request.Query["text"].FirstOrDefault();
        var from = Math.Max(0, int.TryParse(request.Query["from"].FirstOrDefault(), out var f) ? f : 0);
        var size = Math.Max(0, int.TryParse(request.Query["size"].FirstOrDefault(), out var s) ? s : 20);

        var surface = surfaceAccessor.Current!;
        var result = await service.SearchAsync(surface.FeedId, query, from, size, cancellationToken);

        var objects = result.Objects.Select(o => new
        {
            package = new
            {
                name = o.Name,
                version = o.Version,
                description = o.Description,
                date = o.Published?.ToString("o"),
            },
        });

        // Registry wire contracts must retain total=0 even when the host omits default JSON values.
        return Results.Json(new { total = result.Total, objects }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static async Task<IResult> Login(
        HttpRequest request,
        IPackageAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaxLoginBodyBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Login payload too large.");
        }

        try
        {
            await using var body = new MemoryStream();
            await request.Body.CopyToWithLimitAsync(body, MaxLoginBodyBytes, cancellationToken);
            body.Position = 0;

            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("password", out var passwordElement)
                || passwordElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(passwordElement.GetString()))
            {
                return Results.BadRequest(new { error = "A feed API token is required as the password." });
            }

            var token = passwordElement.GetString()!;
            var result = await authentication.AuthenticateForOperationAsync(
                token,
                FeedOperation.Login,
                cancellationToken);

            return result.Succeeded
                ? Results.Json(new { ok = true, token })
                : Results.Unauthorized();
        }
        catch (NpmPublishSizeLimitExceededException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Login payload too large.");
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Invalid login payload." });
        }
    }

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
        var eventContext = new FeedArtifactEventContext(
            surface,
            NpmPackageService.NormalizePackageName(packagePath),
            ExtractVersionFromTarball(tarball),
            tarball);

        if (actionHandler is not null
            && !await actionHandler.CanAccessArtifact(eventContext, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        Stream? stream;
        try
        {
            stream = await service.GetTarballAsync(
                surface.FeedId,
                packagePath,
                tarball,
                surface.PublicBaseUrl,
                cancellationToken);
        }
        catch (NpmTarballSizeLimitExceededException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Package exceeds the configured tarball size limit", detail: exception.Message);
        }

        if (stream is null)
        {
            return Results.NotFound();
        }

        if (actionHandler is not null)
        {
            await actionHandler.OnArtifactDownloaded(eventContext, cancellationToken);
        }

        return Results.Stream(stream, "application/octet-stream");
    }

    private static async Task<IResult> Publish(
        string packagePath,
        HttpRequest request,
        INpmPackageService service,
        ISurfaceContextAccessor surfaceAccessor,
        IFeedActionHandler? actionHandler,
        IOptions<NpmFeedOptions> npmOptions,
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

        var limits = npmOptions.Value;
        if (request.ContentLength.HasValue && request.ContentLength.Value > limits.MaxPublishBodyBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Publish payload too large.");
        }

        var surface = surfaceAccessor.Current!;
        var hasRevision = TryParseRevisionPath(packagePath, out var revisionPath, out var revision);
        var packageName = NpmPackageService.NormalizePackageName(hasRevision ? revisionPath : packagePath);

        try
        {
            await using var body = new MemoryStream();
            await request.Body.CopyToWithLimitAsync(body, limits.MaxPublishBodyBytes, cancellationToken);
            body.Position = 0;

            var document = await JsonNode.ParseAsync(body, cancellationToken: cancellationToken);
            if (document is not JsonObject root) return Results.BadRequest(new { error = "Invalid registry JSON payload." });
            if (GetAttachmentsObject(root) is null)
            {
                if (hasRevision) await service.UnpublishAsync(surface.FeedId, packageName, revision, root, cancellationToken);
                else await service.UpdateDeprecationsAsync(surface.FeedId, packageName, root, cancellationToken);
                return Results.Json(new { ok = true });
            }
            if (hasRevision) return Results.BadRequest(new { error = "Unpublish requests cannot contain new artifacts." });
            var parsed = TryParsePublishBody(root, limits.MaxTarballBytes);
            if (parsed is null)
            {
                return Results.BadRequest(new { error = "Invalid publish payload." });
            }

            var (metadata, tarballStream, distTag) = parsed.Value;
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
                    cancellationToken,
                    distTag);
            }

            if (actionHandler is not null)
            {
                var eventContext = new FeedArtifactEventContext(surface, packageName, version, null);
                await actionHandler.OnArtifactUploaded(eventContext, cancellationToken);
            }

            return Results.Created($"{surface.PublicBaseUrl}{packagePath}", new { ok = true });
        }
        catch (NpmRegistryException exception) { return RegistryError(exception); }
        catch (JsonException) { return Results.BadRequest(new { error = "Invalid registry JSON payload." }); }
        catch (NpmPublishSizeLimitExceededException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Publish payload too large.");
        }
    }

    private static (JsonObject Metadata, Stream Tarball, string DistTag)? TryParsePublishBody(
        JsonObject root,
        long maxTarballBytes)
    {
        try
        {
            var attachments = GetAttachmentsObject(root);
            if (attachments is { Count: 1 })
            {
                var first = attachments.First();
                var attachment = first.Value as JsonObject;
                var data = attachment?["data"]?.GetValue<string>();
                if (string.IsNullOrEmpty(data))
                {
                    return null;
                }

                var bytes = Convert.FromBase64String(data);
                if (bytes.Length > maxTarballBytes)
                {
                    throw new NpmPublishSizeLimitExceededException(
                        $"Tarball exceeds the configured limit of {maxTarballBytes} bytes.");
                }

                var versions = root["versions"] as JsonObject;
                if (versions is not { Count: 1 }) return null;
                var versionKey = versions.First().Key;
                var metadata = versions?[versionKey] as JsonObject ?? new JsonObject();
                if (metadata["version"] is null)
                {
                    metadata["version"] = versionKey;
                }

                if (metadata["name"] is null)
                {
                    metadata["name"] = root["name"]?.GetValue<string>() ?? "package";
                }

                NpmPackageService.CopyPackageMetadata(root, metadata);

                var tags = root["dist-tags"] as JsonObject;
                if (tags is not null && (tags.Count != 1 || tags.First().Value?.GetValue<string>() != versionKey)) return null;
                if (metadata["version"]?.GetValue<string>() != versionKey) return null;
                return (metadata, new MemoryStream(bytes), tags?.First().Key ?? "latest");
            }

            return null;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
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
