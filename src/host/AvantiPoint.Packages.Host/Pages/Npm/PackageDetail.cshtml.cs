using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AvantiPoint.Packages.Host.Pages.Npm;

public class PackageDetailModel(
    IFeedRegistry feedRegistry,
    IHttpContextAccessor httpContextAccessor,
    IPublicBaseUrlProvider publicBaseUrlProvider) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string PackageName { get; set; } = string.Empty;

    public string RegistryUrl { get; private set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Compare { get; set; }

    [BindProperty(SupportsGet = true, Name = "file")]
    public string? SelectedFile { get; set; }

    public string PageTitle => string.IsNullOrWhiteSpace(PackageName)
        ? "npm package"
        : $"{PackageName} (npm)";

    public IActionResult OnGet()
    {
        if (feedRegistry.TryGetNpmSurface() is null)
        {
            return NotFound();
        }

        RegistryUrl = ResolveRegistryUrl();
        return Page();
    }

    private string ResolveRegistryUrl()
    {
        var npmSurface = feedRegistry.TryGetNpmSurface();
        if (npmSurface is null)
        {
            return "/npm/";
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return $"{npmSurface.RoutePrefix}/";
        }

        var baseUrl = publicBaseUrlProvider.GetSurfacePublicBaseUrl(httpContext, npmSurface.RoutePrefix);
        return baseUrl.ToString().TrimEnd('/') + "/";
    }
}
