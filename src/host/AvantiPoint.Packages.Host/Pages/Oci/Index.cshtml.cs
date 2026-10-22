using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Pages.Oci;

public class IndexModel(
    IFeedRegistry feedRegistry,
    IOptionsMonitor<OciFeedOptions> ociOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Segment { get; set; }

    public bool HasOciSurface { get; private set; }

    public string PageTitle { get; private set; } = "OCI";

    public string EcosystemName { get; private set; } = "OCI";

    public string Description { get; private set; } = string.Empty;

    public string ConnectHref => string.IsNullOrEmpty(Segment)
        ? "/oci/connect"
        : $"/oci/connect/{Uri.EscapeDataString(Segment)}";

    public IActionResult OnGet()
    {
        var registration = string.IsNullOrEmpty(Segment)
            ? feedRegistry.TryGetDefaultOciSurface()
            : feedRegistry.TryGetOciSurfaceBySegment(Segment);
        if (registration is null)
        {
            return NotFound();
        }

        HasOciSurface = true;
        Segment = registration.OciSegment;
        var options = ociOptions.Get(GetOptionsName());
        PageTitle = OciSurfacePresentation.GetDisplayName(options, Segment);
        EcosystemName = OciSurfacePresentation.GetEcosystemName(options);
        Description = OciSurfacePresentation.GetBrowseDescription(options);

        return Page();
    }

    private string GetOptionsName() => string.IsNullOrEmpty(Segment)
        ? "OciFeed:default"
        : OciSurfaceOptionsBuilder.GetOptionsName(Segment);
}
