using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AvantiPoint.Packages.Host.Pages.Npm;

public class PackageDetailModel(
    INpmPackageBrowseService browseService,
    IFeedRegistry feedRegistry,
    IHttpContextAccessor httpContextAccessor,
    IPublicBaseUrlProvider publicBaseUrlProvider) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string PackageName { get; set; } = string.Empty;

    public NpmPackageDetailModel? Package { get; private set; }
    public bool IsLoading { get; private set; } = true;
    public string RegistryUrl { get; private set; } = string.Empty;

    public string PageTitle => string.IsNullOrWhiteSpace(Package?.Name)
        ? "npm package"
        : $"{Package.Name} (npm)";

    public async Task<IActionResult> OnGetAsync()
    {
        if (feedRegistry.TryGetNpmSurface() is null)
        {
            return NotFound();
        }

        IsLoading = true;
        Package = await browseService.GetPackageAsync(PackageName);
        RegistryUrl = ResolveRegistryUrl();
        IsLoading = false;
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
