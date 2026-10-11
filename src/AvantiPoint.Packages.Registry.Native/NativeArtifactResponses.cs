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
        NativeArtifactStore store, IFeedActionHandler? handler, CancellationToken ct,
        FeedArtifactEventContext? authorizedEvent = null)
    {
        if (artifact is null) return Results.NotFound();
        var evt = authorizedEvent ?? Event(surface, artifact);
        if (authorizedEvent is null && handler is not null && !await handler.CanAccessArtifact(evt, ct))
            return Results.StatusCode(403);
        if (HttpMethods.IsHead(http.Request.Method))
        {
            http.Response.ContentLength = artifact.Length;
            http.Response.ContentType = artifact.ContentType;
            http.Response.Headers.ETag = $"\"{artifact.ContentHash}\"";
            return Results.Ok();
        }
        if (handler is not null) await handler.OnArtifactDownloaded(evt, ct);
        return Results.Stream(destination => store.CopyToAsync(artifact, destination, ct), artifact.ContentType,
            entityTag: new EntityTagHeaderValue($"\"{artifact.ContentHash}\""));
    }

    public static FeedArtifactEventContext Event(SurfaceContext surface, NativeArtifact artifact) =>
        new(surface, artifact.PackageName, artifact.Version, artifact.Path);
}
