using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AvantiPoint.Packages.Host.Pages.Oci;

public class IndexModel(IFeedRegistry feedRegistry) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Segment { get; set; }

    public bool HasOciSurface { get; private set; }

    public string PageTitle => string.IsNullOrEmpty(Segment)
        ? "OCI catalog"
        : $"OCI catalog ({Segment})";

    public IActionResult OnGet()
    {
        HasOciSurface = string.IsNullOrEmpty(Segment)
            ? feedRegistry.TryGetDefaultOciSurface() is not null
            : feedRegistry.TryGetOciSurfaceBySegment(Segment) is not null;

        return HasOciSurface ? Page() : NotFound();
    }
}
