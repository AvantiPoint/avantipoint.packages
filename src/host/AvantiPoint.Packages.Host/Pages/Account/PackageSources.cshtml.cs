using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class PackageSourcesModel(
    IContext context,
    IPackageSourceService packageSourceService) : PageModel
{
    public IList<PackageSource> Sources { get; private set; } = [];

    [BindProperty]
    public PackageSourceInput Input { get; set; } = new();

    [BindProperty]
    public int? EditId { get; set; }

    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(int? editId)
    {
        EditId = editId;
        await LoadSourcesAsync();
        if (editId.HasValue)
        {
            var source = Sources.FirstOrDefault(s => s.Id == editId.Value);
            if (source != null)
            {
                Input = PackageSourceInput.FromEntity(source);
            }
        }
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadSourcesAsync();
            return Page();
        }

        if (EditId.HasValue)
        {
            var existing = await context.PackageSources.FirstAsync(s => s.Id == EditId.Value);
            Input.ApplyTo(existing);
            existing.LastModifiedAt = DateTimeOffset.UtcNow;
            await packageSourceService.UpdateAsync(existing);
            StatusMessage = $"Updated source '{existing.Name}'.";
        }
        else
        {
            var source = Input.ToEntity();
            await packageSourceService.AddAsync(source);
            StatusMessage = $"Added source '{source.Name}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRefreshMetadataAsync(int id)
    {
        await packageSourceService.RefreshMetadataAsync(id);
        StatusMessage = "Metadata refreshed.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleEnabledAsync(int id, bool enable)
    {
        var source = await context.PackageSources.FirstAsync(s => s.Id == id);
        source.IsEnabled = enable;
        source.LastModifiedAt = DateTimeOffset.UtcNow;
        await packageSourceService.UpdateAsync(source);
        return RedirectToPage();
    }

    private async Task LoadSourcesAsync() =>
        Sources = await context.PackageSources.OrderBy(s => s.Name).ToListAsync();

    public sealed class PackageSourceInput
    {
        [Required]
        [StringLength(128)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Url]
        public string FeedUrl { get; set; } = string.Empty;

        public PackageSourceType Type { get; set; } = PackageSourceType.Upstream;

        public PackageSourceCachingStrategy CachingStrategy { get; set; } =
            PackageSourceCachingStrategy.IndexAndCache;

        public bool IsEnabled { get; set; } = true;

        public string? Username { get; set; }

        public string? Password { get; set; }

        public string? ApiKey { get; set; }

        public static PackageSourceInput FromEntity(PackageSource source) => new()
        {
            Name = source.Name,
            FeedUrl = source.FeedUrl,
            Type = source.Type,
            CachingStrategy = source.CachingStrategy,
            IsEnabled = source.IsEnabled,
            Username = source.Username,
            Password = source.Password,
            ApiKey = source.ApiKey,
        };

        public PackageSource ToEntity() => ApplyTo(new PackageSource());

        public PackageSource ApplyTo(PackageSource source)
        {
            source.Name = Name.Trim();
            source.FeedUrl = FeedUrl.Trim();
            source.Type = Type;
            source.CachingStrategy = CachingStrategy;
            source.IsEnabled = IsEnabled;
            source.Username = Username;
            source.Password = Password;
            source.ApiKey = ApiKey;
            return source;
        }
    }
}
