using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace AvantiPoint.Packages.Registry.Native;

internal static class NativeArtifactResponses
{
    public static async Task<IResult> DownloadAsync(
        HttpContext http, SurfaceContext surface, NativeArtifact? artifact,
        NativeArtifactStore store, IFeedActionHandler? handler, CancellationToken ct)
    {
        if (artifact is null) return Results.NotFound();
        var evt = Event(surface, artifact);
        if (handler is not null && !await handler.CanAccessArtifact(evt, ct))
            return Results.StatusCode(403);
        if (HttpMethods.IsHead(http.Request.Method))
        {
            http.Response.ContentLength = artifact.Length;
            http.Response.ContentType = artifact.ContentType;
            http.Response.Headers.ETag = $"\"{artifact.ContentHash}\"";
            return Results.Ok();
        }
        var stream = await store.OpenAsync(artifact, ct);
        if (handler is not null) await handler.OnArtifactDownloaded(evt, ct);
        return Results.Stream(stream, artifact.ContentType,
            entityTag: new EntityTagHeaderValue($"\"{artifact.ContentHash}\""), enableRangeProcessing: stream.CanSeek);
    }

    public static FeedArtifactEventContext Event(SurfaceContext surface, NativeArtifact artifact) =>
        new(surface, artifact.PackageName, artifact.Version, "sha256:" + artifact.ContentHash);
}
