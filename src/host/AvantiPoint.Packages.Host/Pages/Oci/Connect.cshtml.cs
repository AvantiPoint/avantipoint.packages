using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Pages.Oci;

public class ConnectModel(
    IFeedRegistry feedRegistry,
    IOptionsMonitor<OciFeedOptions> ociOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Segment { get; set; }

    public string PageTitle => ResolveProfile() switch
    {
        OciProfile.ContainerImages => "Connect to the Docker registry",
        OciProfile.HelmCharts => "Connect to the Helm registry",
        _ => "Connect to the OCI registry",
    };

    public string CatalogLabel => ResolveProfile() switch
    {
        OciProfile.ContainerImages => "Container images",
        OciProfile.HelmCharts => "Helm charts",
        _ => "OCI artifacts",
    };

    public string CatalogHref => string.IsNullOrEmpty(Segment) ? "/oci" : $"/oci/{Segment}";

    public IActionResult OnGet()
    {
        var hasSurface = string.IsNullOrEmpty(Segment)
            ? feedRegistry.TryGetDefaultOciSurface() is not null
            : feedRegistry.TryGetOciSurfaceBySegment(Segment) is not null;

        return hasSurface ? Page() : NotFound();
    }

    private OciProfile ResolveProfile() => ociOptions.Get(GetOptionsName()).Profile;

    private string GetOptionsName() => string.IsNullOrEmpty(Segment)
        ? "OciFeed:default"
        : OciSurfaceOptionsBuilder.GetOptionsName(Segment);
}
