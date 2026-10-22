using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AvantiPoint.Packages.Host.Pages;

public class PackageDetailModel(
    IPackageMetadataService metadataService,
    IHttpContextAccessor httpContextAccessor,
    IPublicBaseUrlProvider publicBaseUrlProvider) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string PackageId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? PackageVersion { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Compare { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Install { get; set; }

    [BindProperty(SupportsGet = true, Name = "file")]
    public string? FilePath { get; set; }

    [BindProperty(SupportsGet = true, Name = "view")]
    public string? View { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tfm { get; set; }

    public PackageInfoCollection? PackageInfo { get; set; }
    public bool IsLoading { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public string PageTitle => string.IsNullOrWhiteSpace(PackageInfo?.PackageId)
        ? "Package"
        : PackageInfo.PackageId;

    public string FeedBaseUrl { get; private set; } = string.Empty;

    public string? ReadmeMarkdown { get; private set; }

    public async Task OnGetAsync()
    {
        ErrorMessage = null;
        PackageInfo = null;
        ReadmeMarkdown = null;

        if (string.IsNullOrWhiteSpace(PackageId))
        {
            ErrorMessage = "Package id is required.";
            IsLoading = false;
            return;
        }

        IsLoading = true;

        try
        {
            var version = string.IsNullOrWhiteSpace(PackageVersion) ? string.Empty : PackageVersion;
            PackageInfo = await metadataService.GetPackageInfo(PackageId, version, HttpContext.RequestAborted);

            if (PackageInfo?.Versions?.Any() != true)
            {
                ErrorMessage = $"Package '{PackageId}' was not found.";
                PackageInfo = null;
            }

        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load package '{PackageId}': {ex.Message}";
        }
        finally
        {
            FeedBaseUrl = httpContextAccessor.HttpContext is not null
                ? publicBaseUrlProvider.GetRequestOrigin(httpContextAccessor.HttpContext).ToString().TrimEnd('/')
                : string.Empty;
            IsLoading = false;
        }
    }

}
