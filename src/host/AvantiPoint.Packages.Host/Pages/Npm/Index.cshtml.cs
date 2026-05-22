using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AvantiPoint.Packages.Host.Pages.Npm;

public class IndexModel(IFeedRegistry feedRegistry) : PageModel
{
    public IActionResult OnGet()
    {
        return feedRegistry.TryGetNpmSurface() is not null ? Page() : NotFound();
    }
}
