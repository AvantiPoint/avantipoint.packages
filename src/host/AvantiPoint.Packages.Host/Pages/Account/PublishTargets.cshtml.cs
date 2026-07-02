using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class PublishTargetsModel(
    IHostIdentityContext context,
    ISecretProtector secretProtector) : PageModel
{
    public IList<HostPublishTarget> Targets { get; private set; } = [];

    [BindProperty]
    public PublishTargetInput Input { get; set; } = new();

    [BindProperty]
    public string? EditName { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(string? editName)
    {
        EditName = editName;
        await LoadTargetsAsync();
        if (!string.IsNullOrEmpty(editName))
        {
            var target = Targets.FirstOrDefault(t => t.Name == editName);
            if (target is not null)
            {
                Input = new PublishTargetInput
                {
                    Name = target.Name,
                    PublishEndpoint = target.PublishEndpoint,
                    Legacy = target.Legacy,
                };
            }
        }
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadTargetsAsync();
            return Page();
        }

        if (!string.IsNullOrEmpty(EditName))
        {
            var existing = await context.HostPublishTargets.FirstOrDefaultAsync(t => t.Name == EditName);
            if (existing is null)
            {
                StatusMessage = $"Target '{EditName}' no longer exists.";
                return RedirectToPage();
            }

            existing.PublishEndpoint = Input.PublishEndpoint.Trim();
            existing.Legacy = Input.Legacy;
            if (!string.IsNullOrWhiteSpace(Input.ApiToken))
            {
                // Blank token keeps the existing secret.
                existing.ApiToken = secretProtector.Protect(Input.ApiToken)!;
            }

            await context.SaveChangesAsync(default);
            StatusMessage = $"Updated target '{existing.Name}'.";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Input.ApiToken))
            {
                ModelState.AddModelError("Input.ApiToken", "An API token is required for new targets.");
                await LoadTargetsAsync();
                return Page();
            }

            context.HostPublishTargets.Add(new HostPublishTarget
            {
                Name = Input.Name.Trim(),
                PublishEndpoint = Input.PublishEndpoint.Trim(),
                ApiToken = secretProtector.Protect(Input.ApiToken)!,
                Legacy = Input.Legacy,
                AddedBy = User.Identity?.Name ?? "admin",
                Timestamp = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync(default);
            StatusMessage = $"Added target '{Input.Name}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string name)
    {
        var target = await context.HostPublishTargets
            .Include(t => t.Syndications)
            .FirstOrDefaultAsync(t => t.Name == name);
        if (target is not null)
        {
            context.HostPackageGroupSyndications.RemoveRange(target.Syndications);
            context.HostPublishTargets.Remove(target);
            await context.SaveChangesAsync(default);
            StatusMessage = $"Deleted target '{name}'.";
        }

        return RedirectToPage();
    }

    private async Task LoadTargetsAsync() =>
        Targets = await context.HostPublishTargets.OrderBy(t => t.Name).ToListAsync();

    public sealed class PublishTargetInput
    {
        [Required]
        [StringLength(128)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Url]
        public string PublishEndpoint { get; set; } = string.Empty;

        public string? ApiToken { get; set; }

        public bool Legacy { get; set; }
    }
}
