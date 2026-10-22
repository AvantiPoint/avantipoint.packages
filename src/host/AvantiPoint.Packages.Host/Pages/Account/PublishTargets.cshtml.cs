using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Publishers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class PublishTargetsModel(
    IHostIdentityContext context,
    ISecretProtector secretProtector,
    IEnumerable<IDownstreamPublisher> publishers) : PageModel
{
    public IList<HostPublishTarget> Targets { get; private set; } = [];

    /// <summary>
    /// Protocols with a registered <see cref="IDownstreamPublisher"/>. Only these are offered
    /// when creating/editing a target, so an admin can't create a target that can never be
    /// promoted (PushToSourceAsync throws for a protocol with no publisher).
    /// </summary>
    public IReadOnlyList<PublishTargetProtocol> SupportedProtocols { get; } =
        publishers.Select(p => p.Protocol).Distinct().OrderBy(p => p).ToList();

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
                    Protocol = target.Protocol,
                    Username = target.Username,
                };
            }
        }
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (Input.Protocol != PublishTargetProtocol.Oci) Input.Username = null;
        if (!ModelState.IsValid)
        {
            await LoadTargetsAsync();
            return Page();
        }

        if (!SupportedProtocols.Contains(Input.Protocol))
        {
            ModelState.AddModelError(
                "Input.Protocol",
                $"No publisher is registered for '{Input.Protocol}'. Supported protocols: {string.Join(", ", SupportedProtocols)}.");
            await LoadTargetsAsync();
            return Page();
        }

        if (Input.Protocol == PublishTargetProtocol.NuGet && LooksLikeNuGetWebsiteRootInsteadOfServiceIndex(Input.PublishEndpoint))
        {
            ModelState.AddModelError(
                "Input.PublishEndpoint",
                "This looks like the NuGet.org website, not its v3 service index. Use https://api.nuget.org/v3/index.json instead.");
            await LoadTargetsAsync();
            return Page();
        }

        if (Input.Protocol == PublishTargetProtocol.Oci && LooksLikeOciApiPath(Input.PublishEndpoint))
        {
            ModelState.AddModelError(
                "Input.PublishEndpoint",
                "Use the registry or namespace URL without the /v2 API path. For example, use https://ghcr.io/owner.");
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

            if (existing.Protocol != Input.Protocol && string.IsNullOrWhiteSpace(Input.ApiToken))
            {
                ModelState.AddModelError("Input.ApiToken", "Enter credentials for the new protocol; the previous token will not be reused across ecosystems.");
                await LoadTargetsAsync();
                return Page();
            }

            existing.PublishEndpoint = Input.PublishEndpoint.Trim();
            existing.Protocol = Input.Protocol;
            existing.Username = NormalizeUsername(Input.Username);
            if (!string.IsNullOrWhiteSpace(Input.ApiToken))
            {
                // Blank token keeps the existing secret.
                existing.ApiToken = secretProtector.Protect(Input.ApiToken)!;
            }

            context.Audit(User.Identity?.Name, "publish-target.updated", existing.Name, $"protocol={existing.Protocol}");
            await context.SaveChangesAsync(default);
            StatusMessage = $"Updated target '{existing.Name}'.";
        }
        else
        {
            if (await context.HostPublishTargets.AnyAsync(t => t.Name == Input.Name.Trim()))
            {
                ModelState.AddModelError("Input.Name", "A publish target with this name already exists. Edit it or choose another name.");
                await LoadTargetsAsync();
                return Page();
            }
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
                Protocol = Input.Protocol,
                Username = NormalizeUsername(Input.Username),
                ApiToken = secretProtector.Protect(Input.ApiToken)!,
                AddedBy = User.Identity?.Name ?? "admin",
                Timestamp = DateTimeOffset.UtcNow,
            });
            context.Audit(User.Identity?.Name, "publish-target.created", Input.Name.Trim(), $"protocol={Input.Protocol}");
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
            context.Audit(User.Identity?.Name, "publish-target.deleted", name);
            await context.SaveChangesAsync(default);
            StatusMessage = $"Deleted target '{name}'.";
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Catches the common mistake of pointing a target at the NuGet.org website instead of
    /// its v3 service index; <see cref="AvantiPoint.Packages.Protocol.NuGetClient"/> requires
    /// the service index URL and fails discovery against the website root.
    /// </summary>
    private static bool LooksLikeNuGetWebsiteRootInsteadOfServiceIndex(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var isNuGetOrgHost = host is "nuget.org" or "www.nuget.org";
        var looksLikeServiceIndex = uri.AbsolutePath.Contains("index.json", StringComparison.OrdinalIgnoreCase);

        return isNuGetOrgHost && !looksLikeServiceIndex;
    }

    private static bool LooksLikeOciApiPath(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.AbsolutePath.TrimEnd('/').EndsWith("/v2", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeUsername(string? username) =>
        string.IsNullOrWhiteSpace(username) ? null : username.Trim();

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

        public PublishTargetProtocol Protocol { get; set; } = PublishTargetProtocol.NuGet;

        [StringLength(256)]
        public string? Username { get; set; }

        public string? ApiToken { get; set; }
    }
}
