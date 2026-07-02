using System.ComponentModel.DataAnnotations;
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
    ILogger<PackageGroupsModel> logger) : PageModel
{
    public IList<HostPackageGroup> Groups { get; private set; } = [];

    public IList<HostPublishTarget> Targets { get; private set; } = [];

    [BindProperty]
    [Required]
    [StringLength(128)]
    public string? NewGroupName { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
        {
            await LoadAsync();
            return Page();
        }

        var name = NewGroupName.Trim();
        if (!await context.HostPackageGroups.AnyAsync(g => g.Name == name))
        {
            context.HostPackageGroups.Add(new HostPackageGroup { Name = name });
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
            await context.SaveChangesAsync(default);
            StatusMessage = $"Deleted group '{groupName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddMemberAsync(string groupName, string packageId)
    {
        if (!string.IsNullOrWhiteSpace(packageId))
        {
            packageId = packageId.Trim();
            var exists = await context.HostPackageGroupMembers
                .AnyAsync(m => m.PackageGroupName == groupName && m.PackageId == packageId);
            if (!exists)
            {
                context.HostPackageGroupMembers.Add(new HostPackageGroupMember
                {
                    PackageGroupName = groupName,
                    PackageId = packageId,
                });
                await context.SaveChangesAsync(default);
                StatusMessage = $"Added '{packageId}' to '{groupName}'.";
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveMemberAsync(string groupName, string packageId)
    {
        var member = await context.HostPackageGroupMembers
            .FirstOrDefaultAsync(m => m.PackageGroupName == groupName && m.PackageId == packageId);
        if (member is not null)
        {
            context.HostPackageGroupMembers.Remove(member);
            await context.SaveChangesAsync(default);
            StatusMessage = $"Removed '{packageId}' from '{groupName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddSyndicationAsync(string groupName, string targetName)
    {
        var exists = await context.HostPackageGroupSyndications
            .AnyAsync(s => s.PackageGroupName == groupName && s.PublishTargetName == targetName);
        if (!exists && await context.HostPublishTargets.AnyAsync(t => t.Name == targetName))
        {
            context.HostPackageGroupSyndications.Add(new HostPackageGroupSyndication
            {
                PackageGroupName = groupName,
                PublishTargetName = targetName,
            });
            await context.SaveChangesAsync(default);
            StatusMessage = $"'{groupName}' now syndicates to '{targetName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveSyndicationAsync(string groupName, string targetName)
    {
        var syndication = await context.HostPackageGroupSyndications
            .FirstOrDefaultAsync(s => s.PackageGroupName == groupName && s.PublishTargetName == targetName);
        if (syndication is not null)
        {
            context.HostPackageGroupSyndications.Remove(syndication);
            await context.SaveChangesAsync(default);
            StatusMessage = $"'{groupName}' no longer syndicates to '{targetName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPromoteAsync(string groupName, string targetName)
    {
        try
        {
            var result = await syndicationService.PushToSourceAsync(groupName, targetName);
            StatusMessage = result.AllSucceeded
                ? $"Promoted group '{groupName}' to '{targetName}' ({result.PushedPackageIds.Count} package(s))."
                : $"Promotion of '{groupName}' to '{targetName}' had failures: " +
                  $"{result.PushedPackageIds.Count} succeeded, {result.FailedPackageIds.Count} failed " +
                  $"({string.Join(", ", result.FailedPackageIds)}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to promote group {Group} to {Target}", groupName, targetName);
            StatusMessage = $"Promotion of '{groupName}' to '{targetName}' failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Groups = await context.HostPackageGroups
            .Include(g => g.Members)
            .Include(g => g.Syndications)
            .OrderBy(g => g.Name)
            .ToListAsync();
        Targets = await context.HostPublishTargets.OrderBy(t => t.Name).ToListAsync();
    }
}
