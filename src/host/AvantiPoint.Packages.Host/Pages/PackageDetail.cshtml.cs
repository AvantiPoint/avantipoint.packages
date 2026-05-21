using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AvantiPoint.Packages.Host.Pages;

public class PackageDetailModel(IPackageMetadataService metadataService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string PackageId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? PackageVersion { get; set; }

    public PackageInfoCollection? PackageInfo { get; set; }
    public bool IsLoading { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public string PageTitle => string.IsNullOrWhiteSpace(PackageInfo?.PackageId)
        ? "Package"
        : PackageInfo.PackageId;

    public async Task OnGetAsync()
    {
        ErrorMessage = null;
        PackageInfo = null;

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
            PackageInfo = await metadataService.GetPackageInfo(PackageId, version);

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
            IsLoading = false;
        }
    }

    public Task HandleVersionSelected(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return Task.CompletedTask;
        }

        var id = PackageInfo?.PackageId ?? PackageId;
        var destination = $"/packages/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(version)}";
        Response.Redirect(destination);
        return Task.CompletedTask;
    }
}
