using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Registry.Native.Storage;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Feed.Platform.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AvantiPoint.Packages.Host.Pages.Native;

[AllowAnonymous] // The native read guard validates consumer sessions or feed tokens.
public sealed class DownloadModel(NativePackageBrowseService browse, IServiceProvider services,
    IHostIdentityContext identity, IOptions<FeedOptions> options,
    IFeedActionHandler? handler = null) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string protocol, string? package, string? path)
    {
        var ct = HttpContext.RequestAborted;
        var status = await browse.AuthorizeAsync(protocol, ct);
        if (status != 200) return StatusCode(status);
        if (!options.Value.Authentication.AllowAnonymousPull && !Request.Headers.ContainsKey("Authorization"))
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("preferred_username");
            if (email is null || !await identity.HostUsers.AsNoTracking().AnyAsync(user => user.Email == email
                && user.CanConsume && !user.IsRevoked && user.ApprovalStatus == HostUserApprovalStatus.Approved, ct))
                return StatusCode(403);
        }
        if (package is null || path is null) return NotFound();
        var artifact = await browse.GetArtifactAsync(protocol, package, path, ct);
        if (artifact is null) return NotFound();
        var surface = browse.GetSurface(protocol)!;
        if (handler is not null)
            await handler.OnArtifactDownloaded(new(surface, artifact.PackageName, artifact.Version, artifact.Path), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        var store = services.GetRequiredService<NativeArtifactStore>();
        await Results.Stream(stream => store.CopyToAsync(artifact, stream, ct), artifact.ContentType,
            fileDownloadName: System.IO.Path.GetFileName(artifact.Path)).ExecuteAsync(HttpContext);
        return new EmptyResult();
    }
}
