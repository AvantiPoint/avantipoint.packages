using System.Text.RegularExpressions;
using System.Xml;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Native.Authentication;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Registry.Native.Swift;

/// <summary>Hosts binaryTarget URLs; this is not the Swift source-registry protocol.</summary>
public static partial class SwiftBinaryEndpoints
{
    public static void MapSwiftBinaries(this WebApplication app, string prefix)
    {
        var group = app.MapGroup(prefix);
        group.MapMethods("/{package}/{version}/{module}.xcframework.zip", ["GET", "HEAD"], Download)
            .AddEndpointFilter(new NativeAuthorizationFilter(FeedProtocol.Swift, FeedOperation.Pull));
        group.MapPut("/{package}/{version}/{module}.xcframework.zip", Publish)
            .AddEndpointFilter(new NativeAuthorizationFilter(FeedProtocol.Swift, FeedOperation.Push));
        group.MapGet("/{package}/{version}/index.json", Index)
            .AddEndpointFilter(new NativeAuthorizationFilter(FeedProtocol.Swift, FeedOperation.Pull));
    }

    private static async Task<IResult> Download(string package, string version, string module,
        HttpContext http, NativeArtifactStore store, ISurfaceContextAccessor accessor,
        IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!Valid(package, version, module)) return Results.NotFound();
        var surface = accessor.Current!;
        return await NativeArtifactResponses.DownloadAsync(http, surface,
            await store.FindAsync(surface, Path(package, version, module), ct), store, handler, ct);
    }

    private static async Task<IResult> Index(string package, string version, NativeArtifactStore store,
        ISurfaceContextAccessor accessor, IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!Valid(package, version, "Placeholder")) return Results.NotFound();
        var surface = accessor.Current!;
        if (handler is not null && !await handler.CanAccessArtifact(new(surface, package, version, null), ct))
            return Results.StatusCode(403);
        var artifacts = (await store.ListAsync(surface, package, ct)).Where(a => a.Version == version).ToArray();
        if (artifacts.Length == 0) return Results.NotFound();
        return Results.Json(new
        {
            package, version,
            artifacts = artifacts.OrderBy(a => a.Path, StringComparer.Ordinal).Select(a => new
            {
                module = System.IO.Path.GetFileName(a.Path)[..^".xcframework.zip".Length],
                url = new Uri(surface.PublicBaseUrl, a.Path).AbsoluteUri,
                checksum = a.ContentHash,
            }),
        });
    }

    private static async Task<IResult> Publish(string package, string version, string module,
        HttpContext http, NativeArtifactStore store, ISurfaceContextAccessor accessor,
        IOptionsMonitor<NativeRegistryOptions> options, IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!Valid(package, version, module)) return Results.BadRequest(new { error = "Invalid binary artifact identity." });
        var surface = accessor.Current!;
        var path = Path(package, version, module);
        var evt = new FeedArtifactEventContext(surface, package, version, path);
        if (handler is not null && !await handler.CanAccessArtifact(evt, ct)) return Results.StatusCode(403);
        var limits = options.Get("Swift");
        if (http.Request.ContentLength > limits.MaxArtifactBytes) return Results.StatusCode(413);
        try
        {
            await using var upload = await ArtifactUpload.ReadAsync(http.Request.Body, limits.MaxArtifactBytes, ct);
            XcframeworkValidator.Validate(upload.Stream, module, limits, ct);
            var result = await store.PutAsync(surface, path, package, version, "application/zip", upload, null, ct);
            if (result == StoragePutResult.Conflict) return Results.Conflict(new { error = "Released binaries are immutable." });
            if (result == StoragePutResult.Success && handler is not null) await handler.OnArtifactUploaded(evt, ct);
            return Results.Json(new { checksum = upload.Sha256, url = new Uri(surface.PublicBaseUrl, path).AbsoluteUri },
                statusCode: result == StoragePutResult.Success ? 201 : 200);
        }
        catch (ArtifactTooLargeException) { return Results.StatusCode(413); }
        catch (Exception ex) when (ex is InvalidDataException or XmlException)
        { return Results.BadRequest(new { error = "Invalid binary-only XCFramework archive." }); }
    }

    private static bool Valid(string package, string version, string module) =>
        ArtifactPath.IsValidSegment(package) && package.Length <= 256
        && ArtifactPath.IsValidSegment(version) && version.Length <= 128 && NuGetVersion.TryParse(version, out _)
        && ModulePattern().IsMatch(module);

    private static string Path(string package, string version, string module) => $"{package}/{version}/{module}.xcframework.zip";

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ModulePattern();
}
