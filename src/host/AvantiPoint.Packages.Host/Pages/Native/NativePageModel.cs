using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace AvantiPoint.Packages.Host.Pages.Native;

[AllowAnonymous] // The explicit native guard validates read tokens and consumer sessions.
public sealed class NativePageModel(NativePackageBrowseService browse) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Protocol { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? PackageName { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var status = await browse.AuthorizeAsync(Protocol, HttpContext.RequestAborted);
        return status == 200 ? Page() : StatusCode(status);
    }
}
