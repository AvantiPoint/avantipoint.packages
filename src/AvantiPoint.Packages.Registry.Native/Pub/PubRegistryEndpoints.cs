using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Native.Authentication;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using YamlDotNet.Core;

namespace AvantiPoint.Packages.Registry.Native.Pub;

public static class PubRegistryEndpoints
{
    private const string JsonContentType = "application/vnd.pub.v2+json";

    public static void MapPubRegistry(this WebApplication app, string prefix)
    {
        var group = app.MapGroup(prefix);
        var consume = new NativeAuthorizationFilter(FeedProtocol.Pub, FeedOperation.Pull);
        var publish = new NativeAuthorizationFilter(FeedProtocol.Pub, FeedOperation.Push);
        group.MapGet("/api/packages/versions/new", (ISurfaceContextAccessor accessor) => Json(new
        {
            url = new Uri(accessor.Current!.PublicBaseUrl, "api/packages/versions/upload").AbsoluteUri,
            fields = new Dictionary<string, string>(),
        })).AddEndpointFilter(publish);
        group.MapPost("/api/packages/versions/upload", Upload).AddEndpointFilter(publish);
        group.MapGet("/api/packages/{package}", Versions).AddEndpointFilter(consume);
        group.MapGet("/api/packages/{package}/versions/{version}", Version).AddEndpointFilter(consume);
        group.MapGet("/api/packages/{package}/versions/{version}/finalize", FinalizeUpload).AddEndpointFilter(publish);
        group.MapMethods("/packages/{package}/versions/{version}.tar.gz", ["GET", "HEAD"], Download).AddEndpointFilter(consume);
    }

    private static async Task<IResult> Versions(string package, NativeArtifactStore store,
        ISurfaceContextAccessor accessor, IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!PubArchive.ValidName(package)) return Results.NotFound();
        var surface = accessor.Current!;
        if (!await CanAccess(handler, surface, package, null, ct)) return Results.StatusCode(403);
        var artifacts = await store.ListAsync(surface, package, ct);
        if (artifacts.Count == 0) return Results.NotFound();
        var ordered = artifacts.OrderBy(a => NuGetVersion.Parse(a.Version)).ToArray();
        var latest = ordered.LastOrDefault(a => !NuGetVersion.Parse(a.Version).IsPrerelease) ?? ordered[^1];
        return Json(new { name = package, latest = Metadata(latest, surface), versions = ordered.Select(a => Metadata(a, surface)) });
    }

    private static async Task<IResult> Version(string package, string version, NativeArtifactStore store,
        ISurfaceContextAccessor accessor, IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!Valid(package, version)) return Results.NotFound();
        var surface = accessor.Current!;
        if (!await CanAccess(handler, surface, package, version, ct)) return Results.StatusCode(403);
        var artifact = await store.FindAsync(surface, Path(package, version), ct);
        return artifact is null ? Results.NotFound() : Json(Metadata(artifact, surface));
    }

    private static async Task<IResult> Download(string package, string version, HttpContext http,
        NativeArtifactStore store, ISurfaceContextAccessor accessor, IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!Valid(package, version)) return Results.NotFound();
        var surface = accessor.Current!;
        return await NativeArtifactResponses.DownloadAsync(http, surface,
            await store.FindAsync(surface, Path(package, version), ct), store, handler, ct);
    }

    private static async Task<IResult> Upload(HttpContext http, NativeArtifactStore store,
        ISurfaceContextAccessor accessor, IOptionsMonitor<NativeRegistryOptions> options,
        IFeedActionHandler? handler, CancellationToken ct)
    {
        var limits = options.Get("Pub");
        if (http.Request.ContentLength > limits.MaxArtifactBytes + 64 * 1024) return Error("archive_too_large", 413);
        if (!http.Request.HasFormContentType) return Error("multipart_required");
        try
        {
            var feature = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = limits.MaxArtifactBytes + 64 * 1024;
            http.Features.Set<IFormFeature>(new FormFeature(http.Request, new FormOptions
            {
                MultipartBodyLengthLimit = limits.MaxArtifactBytes, ValueCountLimit = 10,
                MultipartHeadersCountLimit = 16, MultipartHeadersLengthLimit = 16 * 1024,
            }));
            var form = await http.Request.ReadFormAsync(ct);
            if (form.Files.Count != 1 || form.Files[0].Name != "file") return Error("one_archive_required");
            await using var source = form.Files[0].OpenReadStream();
            await using var upload = await ArtifactUpload.ReadAsync(source, limits.MaxArtifactBytes, ct);
            var package = await PubArchive.ReadAsync(upload.Stream, limits, ct);
            var surface = accessor.Current!;
            if (!await CanAccess(handler, surface, package.Name, package.Version, ct)) return Results.StatusCode(403);
            var path = Path(package.Name, package.Version);
            var result = await store.PutAsync(surface, path, package.Name, package.Version,
                "application/gzip", upload, package.PubspecJson, ct);
            if (result == StoragePutResult.Conflict) return Error("version_already_exists", 409);
            if (result == StoragePutResult.Success && handler is not null)
                await handler.OnArtifactUploaded(new(surface, package.Name, package.Version, "sha256:" + upload.Sha256), ct);
            http.Response.Headers.Location = new Uri(surface.PublicBaseUrl,
                $"api/packages/{package.Name}/versions/{package.Version}/finalize?checksum={upload.Sha256}").AbsoluteUri;
            return Results.NoContent();
        }
        catch (ArtifactTooLargeException) { return Error("archive_too_large", 413); }
        catch (Exception ex) when (ex is InvalidDataException or YamlException or JsonException or FormatException)
        { return Error("invalid_archive"); }
    }

    private static async Task<IResult> FinalizeUpload(string package, string version, string checksum,
        NativeArtifactStore store, ISurfaceContextAccessor accessor, IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!Valid(package, version)) return Error("invalid_identity");
        var surface = accessor.Current!;
        if (!await CanAccess(handler, surface, package, version, ct)) return Results.StatusCode(403);
        var artifact = await store.FindAsync(surface, Path(package, version), ct);
        return artifact is null || artifact.ContentHash != checksum ? Error("upload_not_found")
            : Json(new { success = new { message = $"Published {package} {version}." } });
    }

    private static object Metadata(NativeArtifact artifact, SurfaceContext surface) => new
    {
        version = artifact.Version,
        archive_url = new Uri(surface.PublicBaseUrl, artifact.Path).AbsoluteUri,
        archive_sha256 = artifact.ContentHash,
        pubspec = JsonSerializer.Deserialize<JsonElement>(artifact.MetadataJson),
        published = artifact.PublishedUtc.ToString("O"),
    };

    private static Task<bool> CanAccess(IFeedActionHandler? handler, SurfaceContext surface,
        string package, string? version, CancellationToken ct) =>
        handler?.CanAccessArtifact(new(surface, package, version, null), ct) ?? Task.FromResult(true);
    private static bool Valid(string package, string version) => PubArchive.ValidName(package) && PubArchive.ValidVersion(version);
    private static string Path(string package, string version) => $"packages/{package}/versions/{version}.tar.gz";
    private static IResult Json(object value, int status = 200) => Results.Json(value, contentType: JsonContentType, statusCode: status);
    private static IResult Error(string code, int status = 400) => Json(new { error = new { code, message = code.Replace('_', ' ') } }, status);
}
