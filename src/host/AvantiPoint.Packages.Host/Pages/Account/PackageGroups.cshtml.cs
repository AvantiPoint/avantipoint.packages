using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class PackageGroupsModel(
    IHostIdentityContext context,
    ISyndicationService syndicationService,
    ILogger<PackageGroupsModel> logger,
    IContext packages) : PageModel
{
    public IList<HostPackageGroup> Groups { get; private set; } = [];

    public IList<HostPublishTarget> Targets { get; private set; } = [];

    [BindProperty]
    [Required]
    [StringLength(128)]
    public string? NewGroupName { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public bool StatusIsError { get; set; }

    [TempData]
    public string? ActiveGroup { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnGetPackagesAsync(string? query, string ecosystem = "NuGet")
    {
        var term = (query ?? "").Trim().ToLowerInvariant();
        if (term.Length > 128) return BadRequest();
        var names = ecosystem switch
        {
            "NuGet" => packages.Packages.Select(p => p.Id),
            "Npm" => packages.NpmPackages.Where(p => p.Versions.Any()).Select(p => p.Name),
            "Docker" => packages.OciRepositories.Where(p => p.OciSegment == "docker" && p.Tags.Any()).Select(p => p.Name),
            "Helm" => packages.OciRepositories.Where(p => p.OciSegment == "helm" && p.Tags.Any()).Select(p => p.Name),
            _ => null,
        };
        if (names is null) return BadRequest();
        return new JsonResult(await names.Where(n => n.ToLower().Contains(term)).Distinct().OrderBy(n => n).Take(20).ToListAsync());
    }

    public async Task<IActionResult> OnPostCreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName) || NewGroupName.Trim().Length > 128)
        {
            await LoadAsync();
            return Page();
        }

        var name = NewGroupName.Trim();
        ActiveGroup = name;
        if (!await context.HostPackageGroups.AnyAsync(g => g.Name == name))
        {
            context.HostPackageGroups.Add(new HostPackageGroup { Name = name });
            context.Audit(User.Identity?.Name, "group.created", name);
            await context.SaveChangesAsync(default);
            StatusMessage = $"Created group '{name}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteGroupAsync(string groupName)
    {
        var group = await context.HostPackageGroups
            .Include(g => g.Members)
            .Include(g => g.Syndications)
            .FirstOrDefaultAsync(g => g.Name == groupName);
        if (group is not null)
        {
            context.HostPackageGroupMembers.RemoveRange(group.Members);
            context.HostPackageGroupSyndications.RemoveRange(group.Syndications);
            context.HostPackageGroups.Remove(group);
            context.Audit(User.Identity?.Name, "group.deleted", groupName);
            await context.SaveChangesAsync(default);
            StatusMessage = $"Deleted group '{groupName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddMemberAsync(string groupName, string packageId, string ecosystem = "NuGet")
    {
        ActiveGroup = groupName;
        if (!await context.HostPackageGroups.AnyAsync(g => g.Name == groupName)) return NotFound();
        if (!string.IsNullOrWhiteSpace(packageId))
        {
            packageId = packageId.Trim();
            var lower = packageId.ToLowerInvariant();
            var canonical = ecosystem switch
            {
                "NuGet" => await packages.Packages.Where(p => p.Id.ToLower() == lower).Select(p => p.Id).FirstOrDefaultAsync(),
                "Npm" => await packages.NpmPackages.Where(p => p.Name == packageId && p.Versions.Any()).Select(p => p.Name).FirstOrDefaultAsync(),
                "Docker" or "Helm" => await packages.OciRepositories.Where(p => p.Name == packageId && p.OciSegment == ecosystem.ToLower() && p.Tags.Any()).Select(p => p.Name).FirstOrDefaultAsync(),
                _ => null,
            };
            if (canonical is null)
            {
                StatusMessage = "Choose a package that is hosted in the selected ecosystem. No package was added.";
                StatusIsError = true;
                return RedirectToPage();
            }
            packageId = ecosystem switch { "Npm" => "npm:" + canonical, "Docker" or "Helm" => "oci:" + canonical, _ => canonical };
            var exists = await context.HostPackageGroupMembers
                .AnyAsync(m => m.PackageGroupName == groupName && m.PackageId == packageId);
            if (!exists)
            {
                context.HostPackageGroupMembers.Add(new HostPackageGroupMember
                {
                    PackageGroupName = groupName,
                    PackageId = packageId,
                });
                context.Audit(User.Identity?.Name, "group.member-added", groupName, $"package={packageId}");
                await context.SaveChangesAsync(default);
                StatusMessage = $"Added '{packageId}' to '{groupName}'.";
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveMemberAsync(string groupName, string packageId)
    {
        ActiveGroup = groupName;
        var member = await context.HostPackageGroupMembers
            .FirstOrDefaultAsync(m => m.PackageGroupName == groupName && m.PackageId == packageId);
        if (member is not null)
        {
            context.HostPackageGroupMembers.Remove(member);
            context.Audit(User.Identity?.Name, "group.member-removed", groupName, $"package={packageId}");
            await context.SaveChangesAsync(default);
            StatusMessage = $"Removed '{packageId}' from '{groupName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddSyndicationAsync(string groupName, string targetName)
    {
        ActiveGroup = groupName;
        if (!await context.HostPackageGroups.AnyAsync(g => g.Name == groupName)) return NotFound();
        var exists = await context.HostPackageGroupSyndications
            .AnyAsync(s => s.PackageGroupName == groupName && s.PublishTargetName == targetName);
        if (!exists && await context.HostPublishTargets.AnyAsync(t => t.Name == targetName))
        {
            context.HostPackageGroupSyndications.Add(new HostPackageGroupSyndication
            {
                PackageGroupName = groupName,
                PublishTargetName = targetName,
            });
            context.Audit(User.Identity?.Name, "group.syndication-added", groupName, $"target={targetName}");
            await context.SaveChangesAsync(default);
            StatusMessage = $"'{groupName}' now syndicates to '{targetName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveSyndicationAsync(string groupName, string targetName)
    {
        ActiveGroup = groupName;
        var syndication = await context.HostPackageGroupSyndications
            .FirstOrDefaultAsync(s => s.PackageGroupName == groupName && s.PublishTargetName == targetName);
        if (syndication is not null)
        {
            context.HostPackageGroupSyndications.Remove(syndication);
            context.Audit(User.Identity?.Name, "group.syndication-removed", groupName, $"target={targetName}");
            await context.SaveChangesAsync(default);
            StatusMessage = $"'{groupName}' no longer syndicates to '{targetName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPromoteAsync(string groupName, string targetName)
    {
        ActiveGroup = groupName;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            StatusIsError = true;
            StatusMessage = "Select a publish target before pushing.";
            return RedirectToPage();
        }

        var memberCount = await context.HostPackageGroupMembers
            .CountAsync(m => m.PackageGroupName == groupName);
        if (memberCount == 0)
        {
            StatusIsError = true;
            StatusMessage = $"Group '{groupName}' has no packages to push. Add members first.";
            return RedirectToPage();
        }

        try
        {
            var result = await syndicationService.PushToSourceAsync(groupName, targetName);
            StatusIsError = !result.AllSucceeded;
            StatusMessage = FormatPushStatus(groupName, targetName, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to promote group {Group} to {Target}", groupName, targetName);
            StatusIsError = true;
            StatusMessage = $"Push of '{groupName}' to '{targetName}' failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    private static string FormatPushStatus(string groupName, string targetName, SyndicationPushResult result)
    {
        var skippedPart = FormatSkipped(result);
        if (result.AllSucceeded)
        {
            var message = string.Format(
                "Pushed latest of group '{0}' to '{1}' ({2} package(s))",
                groupName,
                targetName,
                result.PushedPackageIds.Count);
            if (skippedPart is not null)
            {
                message += ", " + skippedPart;
            }
            return message + ". Recorded in Audit Log as group.promoted.";
        }

        var parts = new List<string>
        {
            string.Format("{0} pushed", result.PushedPackageIds.Count),
        };
        if (skippedPart is not null)
        {
            parts.Add(skippedPart);
        }
        parts.Add(string.Format(
            "{0} failed ({1})",
            result.FailedPackageIds.Count,
            string.Join(", ", result.FailedPackageIds)));
        return string.Format(
            "Push of '{0}' to '{1}' had failures: {2}. See Audit Log (group.promoted).",
            groupName,
            targetName,
            string.Join(", ", parts));
    }

    private static string? FormatSkipped(SyndicationPushResult result)
    {
        if (result.SkippedPackages.Count == 0)
        {
            return null;
        }

        return string.Format(
            "{0} skipped ({1})",
            result.SkippedPackages.Count,
            string.Join("; ", result.SkippedPackages.Select(s => s.PackageId + ": " + s.Reason)));
    }

    private async Task LoadAsync()
    {
        Groups = await context.HostPackageGroups
            .Include(g => g.Members)
            .Include(g => g.Syndications)
            .AsSplitQuery()
            .OrderBy(g => g.Name)
            .ToListAsync();
        Targets = await context.HostPublishTargets.OrderBy(t => t.Name).ToListAsync();
    }
}
