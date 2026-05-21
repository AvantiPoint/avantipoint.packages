using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AvantiPoint.Packages.Host.Pages.Oci;

public class RepositoryModel(IFeedRegistry feedRegistry, IOciRepositoryBrowseService browseService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Segment { get; set; }

    [BindProperty(SupportsGet = true)]
    public string RepositoryName { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? Reference { get; set; }

    public OciArtifactDetailModel? Artifact { get; private set; }
    public bool HasRepository { get; private set; }
    public bool IsLoading { get; private set; } = true;

    public string PageTitle => string.IsNullOrWhiteSpace(Reference)
        ? RepositoryName
        : $"{RepositoryName}:{Reference}";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!HasRegisteredOciSurface())
        {
            return NotFound();
        }

        IsLoading = true;

        if (!string.IsNullOrWhiteSpace(Reference))
        {
            Artifact = await browseService.GetArtifactAsync(RepositoryName, Reference, Segment);
            HasRepository = Artifact is not null;
        }
        else
        {
            var tags = await browseService.ListTagsAsync(RepositoryName, Segment, max: 100);
            HasRepository = tags is not null;
        }

        IsLoading = false;
        return Page();
    }

    private bool HasRegisteredOciSurface()
    {
        if (string.IsNullOrEmpty(Segment))
        {
            return feedRegistry.TryGetDefaultOciSurface() is not null;
        }

        return feedRegistry.TryGetOciSurfaceBySegment(Segment) is not null;
    }
}
