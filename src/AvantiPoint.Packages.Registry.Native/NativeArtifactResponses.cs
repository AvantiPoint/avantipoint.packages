using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace AvantiPoint.Packages.Registry.Native;

internal static class NativeArtifactResponses
{
    public static async Task<IReadOnlyList<NativeArtifact>> FilterAccessibleAsync(
        SurfaceContext surface, IEnumerable<NativeArtifact> artifacts,
        IFeedActionHandler? handler, CancellationToken ct)
    {
        var visible = new List<NativeArtifact>();
        foreach (var artifact in artifacts)
        {
            ct.ThrowIfCancellationRequested();
            if (handler is null || await handler.CanAccessArtifact(Event(surface, artifact), ct))
                visible.Add(artifact);
        }
        return visible;
    }

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
        return Results.Stream(async destination =>
        {
            await store.CopyToAsync(artifact, destination, ct);
            await destination.FlushAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (handler is not null) await handler.OnArtifactDownloaded(evt, ct);
        }, artifact.ContentType,
            entityTag: new EntityTagHeaderValue($"\"{artifact.ContentHash}\""));
    }

    public static FeedArtifactEventContext Event(SurfaceContext surface, NativeArtifact artifact) =>
        new(surface, artifact.PackageName, artifact.Version, artifact.Path);
}
