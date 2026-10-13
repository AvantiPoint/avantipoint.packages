using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Registry.Native.Storage;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AvantiPoint.Packages.Host.Pages.Native;

[AllowAnonymous] // The native read guard validates consumer sessions or feed tokens.
public sealed class DownloadModel(NativePackageBrowseService browse, IServiceProvider services,
    IFeedActionHandler? handler = null) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string protocol, string? package, string? path)
    {
        var ct = HttpContext.RequestAborted;
        var status = await browse.AuthorizeAsync(protocol, ct);
        if (status != 200) return StatusCode(status);
        if (package is null || path is null) return NotFound();
        var artifact = await browse.GetArtifactAsync(protocol, package, path, ct);
        if (artifact is null) return NotFound();
        var surface = browse.GetSurface(protocol)!;
        Response.Headers.XContentTypeOptions = "nosniff";
        var store = services.GetRequiredService<NativeArtifactStore>();
        await Results.Stream(async stream =>
        {
            await store.CopyToAsync(artifact, stream, ct);
            await stream.FlushAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (handler is not null)
                await handler.OnArtifactDownloaded(new(surface, artifact.PackageName, artifact.Version, artifact.Path), ct);
        }, artifact.ContentType,
            fileDownloadName: System.IO.Path.GetFileName(artifact.Path)).ExecuteAsync(HttpContext);
        return new EmptyResult();
    }
}
