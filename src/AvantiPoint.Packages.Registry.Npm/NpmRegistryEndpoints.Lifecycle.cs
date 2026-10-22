using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Packages.Registry.Npm;

public static partial class NpmRegistryEndpoints
{
    private static bool TryParseRevisionPath(string path, out string artifactPath, out string revision)
    {
        var decoded = Uri.UnescapeDataString(path);
        var marker = decoded.LastIndexOf("/-rev/", StringComparison.Ordinal);
        artifactPath = marker > 0 ? decoded[..marker] : string.Empty;
        revision = marker > 0 ? decoded[(marker + 6)..] : string.Empty;
        return marker > 0 && revision.Length is > 0 and <= 128 && !revision.Contains('/');
    }

    private static async Task<IResult> Unpublish(string packagePath, INpmPackageService service, ISurfaceContextAccessor accessor, CancellationToken cancellationToken)
    {
        if (IsInternalPath(packagePath) || !TryParseRevisionPath(packagePath, out var path, out var revision))
            return Results.BadRequest(new { error = "A package revision is required to unpublish." });
        try
        {
            if (TryParseTarballPath(path, out var name, out var file))
                await service.ConfirmTarballRemovalAsync(accessor.Current!.FeedId, name, file, revision, cancellationToken);
            else await service.UnpublishAsync(accessor.Current!.FeedId, path, revision, null, cancellationToken);
            return Results.Json(new { ok = true });
        }
        catch (NpmRegistryException exception) { return RegistryError(exception); }
    }

    private static bool TryParseDistTagPath(string tagPath, out string package, out string? tag)
    {
        var path = Uri.UnescapeDataString(tagPath);
        var marker = path.LastIndexOf("/dist-tags", StringComparison.Ordinal);
        package = marker > 0 ? path[..marker] : string.Empty;
        var remainder = marker > 0 ? path[(marker + "/dist-tags".Length)..] : string.Empty;
        tag = remainder.StartsWith('/') && remainder.Length > 1 ? remainder[1..] : null;
        return marker > 0 && (remainder.Length == 0 || tag is not null && !tag.Contains('/'));
    }

    private static async Task<IResult> GetDistTags(string tagPath, INpmPackageService service, ISurfaceContextAccessor accessor, CancellationToken cancellationToken)
    {
        if (!TryParseDistTagPath(tagPath, out var package, out var tag) || tag is not null) return Results.NotFound();
        var surface = accessor.Current!;
        var packument = await service.GetPackumentAsync(surface.FeedId, package, surface.PublicBaseUrl, cancellationToken);
        return packument is null ? Results.NotFound() : Results.Json(packument["dist-tags"]);
    }

    private static async Task<IResult> SetDistTag(string tagPath, HttpRequest request, INpmPackageService service, ISurfaceContextAccessor accessor, CancellationToken cancellationToken)
    {
        if (!TryParseDistTagPath(tagPath, out var package, out var tag) || tag is null) return Results.NotFound();
        if (request.ContentLength is > 1024) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        try
        {
            await using var body = new MemoryStream();
            await request.Body.CopyToWithLimitAsync(body, 1024, cancellationToken);
            body.Position = 0;
            var node = await JsonNode.ParseAsync(body, cancellationToken: cancellationToken);
            if (node is not JsonValue value || !value.TryGetValue<string>(out var version))
                return Results.BadRequest(new { error = "The dist-tag value must be a JSON version string." });
            await service.SetDistTagAsync(accessor.Current!.FeedId, package, tag, version, cancellationToken);
            return Results.Json(new { ok = true });
        }
        catch (NpmRegistryException exception) { return RegistryError(exception); }
        catch (NpmPublishSizeLimitExceededException) { return Results.StatusCode(StatusCodes.Status413PayloadTooLarge); }
        catch (JsonException) { return Results.BadRequest(new { error = "Invalid dist-tag JSON." }); }
    }

    private static async Task<IResult> RemoveDistTag(string tagPath, INpmPackageService service, ISurfaceContextAccessor accessor, CancellationToken cancellationToken)
    {
        if (!TryParseDistTagPath(tagPath, out var package, out var tag) || tag is null) return Results.NotFound();
        try
        {
            await service.SetDistTagAsync(accessor.Current!.FeedId, package, tag, null, cancellationToken);
            return Results.Json(new { ok = true });
        }
        catch (NpmRegistryException exception) { return RegistryError(exception); }
    }

    private static IResult RegistryError(NpmRegistryException exception) =>
        Results.Json(new { error = exception.Code, reason = exception.Message }, statusCode: exception.StatusCode);
}
