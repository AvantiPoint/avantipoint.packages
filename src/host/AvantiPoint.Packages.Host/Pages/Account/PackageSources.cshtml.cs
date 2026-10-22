using System.ComponentModel.DataAnnotations;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Events;
using AvantiPoint.Packages.Host.Admin.Services.Secrets;
using AvantiPoint.Packages.Host.Admin.Services.Upstreams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class PackageSourcesModel(
    IContext context,
    IPackageSourceService packageSourceService,
    IPackageSourceConnectionTester connectionTester,
    ISecretProtector secretProtector,
    IFeedRegistry feedRegistry,
    ILogger<PackageSourcesModel> logger,
    HostPublishTargetSourceImporter targetImporter,
    IHostEventService events) : PageModel
{
    public IList<PackageSource> Sources { get; private set; } = [];

    [BindProperty]
    public PackageSourceInput Input { get; set; } = new();

    [BindProperty]
    public int? EditId { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public bool StatusIsError { get; set; }

    public IReadOnlyList<string> OciSurfaces => feedRegistry.Surfaces
        .Where(surface => surface.Protocol == FeedProtocol.Oci)
        .Select(surface => surface.OciSegment)
        .Where(segment => !string.IsNullOrWhiteSpace(segment))
        .Select(segment => segment!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(segment => segment, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<EcosystemOption> EcosystemOptions
    {
        get
        {
            var surfaces = OciSurfaces.ToList();
            if (PackageSourceEcosystem.TryParse(Input.Ecosystem, out _, out var selectedSurface)
                && !string.IsNullOrWhiteSpace(selectedSurface)
                && !surfaces.Contains(selectedSurface, StringComparer.OrdinalIgnoreCase))
            {
                surfaces.Add(selectedSurface);
            }

            var options = new List<EcosystemOption>
            {
                new(PackageSourceEcosystem.NuGet, "NuGet packages"),
                new(PackageSourceEcosystem.Npm, "npm packages"),
            };
            options.AddRange(surfaces
                .OrderBy(surface => surface, StringComparer.OrdinalIgnoreCase)
                .Select(surface => new EcosystemOption(
                    PackageSourceEcosystem.ForOciSurface(surface),
                    PackageSourceEcosystem.GetDisplayName(surface))));
            options.Add(new EcosystemOption(
                PackageSourceEcosystem.Oci,
                "OCI artifacts (all OCI galleries)"));
            return options;
        }
    }

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
        ValidateInput();
        if (Input.Type != PackageSourceType.Upstream)
        {
            var originalType = EditId.HasValue
                ? await context.PackageSources.Where(s => s.Id == EditId.Value).Select(s => (PackageSourceType?)s.Type).SingleOrDefaultAsync()
                : null;
            if (originalType != Input.Type)
                ModelState.AddModelError("Input.Type", "Sources configure upstream pulls. Use Publish Targets to configure downstream publishing.");
        }
        if (!ModelState.IsValid)
        {
            await LoadSourcesAsync();
            return Page();
        }

        if (EditId.HasValue)
        {
            var existing = await context.PackageSources
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == EditId.Value);
            if (existing is null) return NotFound();
            if (Input.CredentialMode == "Keep" && PackageSourceEcosystem.FromSource(existing) != Input.Ecosystem)
            {
                ModelState.AddModelError("Input.CredentialMode", "Choose authentication again when changing ecosystems; saved credentials may not be compatible.");
                await LoadSourcesAsync();
                return Page();
            }
            var existingUsername = existing.Username;
            var existingPassword = existing.Password;
            var existingApiKey = existing.ApiKey;
            Input.ApplyTo(existing);
            existing.Username = Input.CredentialMode == "Keep" ? existingUsername : secretProtector.Protect(Input.Username);
            existing.Password = Input.CredentialMode == "Keep" ? existingPassword : secretProtector.Protect(Input.Password);
            existing.ApiKey = Input.CredentialMode == "Keep" ? existingApiKey : secretProtector.Protect(Input.ApiKey);
            existing.LastModifiedAt = DateTimeOffset.UtcNow;
            if (!await CheckBeforeSaveAsync(existing))
            {
                await LoadSourcesAsync();
                return Page();
            }
            await packageSourceService.UpdateAsync(existing);
            await packageSourceService.UpdateSyncStateAsync(existing.Id, true, null, HttpContext.RequestAborted);
            await events.RecordAsync("source.updated", existing.Name, $"protocol={existing.Protocol}; mode={existing.CachingStrategy}", HttpContext.RequestAborted);
            StatusMessage = $"Updated source '{existing.Name}'. {StatusMessage}";
        }
        else
        {
            var source = Input.ToEntity();
            if (!await CheckBeforeSaveAsync(source))
            {
                await LoadSourcesAsync();
                return Page();
            }
            // New sources join the end. Ordering is managed only through the list.
            var ordered = await context.PackageSources.OrderBy(s => s.Priority).ThenBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(HttpContext.RequestAborted);
            for (var i = 0; i < ordered.Count; i++) ordered[i].Priority = i;
            source.Priority = ordered.Count;
            source.IsEnabled = true;
            source.LastSyncAttemptAt = DateTimeOffset.UtcNow;
            await packageSourceService.AddAsync(source);
            await events.RecordAsync("source.created", source.Name, $"protocol={source.Protocol}; mode={source.CachingStrategy}", HttpContext.RequestAborted);
            StatusMessage = $"Added source '{source.Name}'. {StatusMessage}";
        }

        return RedirectToPage();
    }

    private async Task<bool> CheckBeforeSaveAsync(PackageSource source)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var result = await connectionTester.TestAsync(source, timeout.Token);
            if (result.Succeeded)
            {
                StatusMessage = result.Message;
                return true;
            }
            ModelState.AddModelError("Input.FeedUrl", $"Source not saved. {result.Message}");
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            ModelState.AddModelError("Input.FeedUrl", "Source not saved. The connection test timed out; check the endpoint and try again.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Upstream connection test failed before saving");
            ModelState.AddModelError("Input.FeedUrl", "Source not saved. The connection could not be verified; check the endpoint and credentials.");
        }
        return false;
    }

    public async Task<IActionResult> OnPostMoveAsync(int id, string direction)
    {
        if (direction is not ("up" or "down")) return BadRequest();
        var ordered = await context.PackageSources.OrderBy(s => s.Priority).ThenBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(HttpContext.RequestAborted);
        var index = ordered.FindIndex(s => s.Id == id);
        if (index < 0) return NotFound();
        var destination = index + (direction == "up" ? -1 : 1);
        if (destination < 0 || destination >= ordered.Count) return RedirectToPage();
        var source = ordered[index];
        (ordered[index], ordered[destination]) = (ordered[destination], ordered[index]);
        for (var i = 0; i < ordered.Count; i++) ordered[i].Priority = i;
        await context.SaveChangesAsync(HttpContext.RequestAborted);
        await events.RecordAsync("source.reordered", source.Name, $"position={destination + 1}", HttpContext.RequestAborted);
        StatusMessage = $"Moved '{source.Name}' {direction}. Sources are tried from top to bottom within their ecosystem.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreatePublishTargetAsync(int id)
    {
        var result = await targetImporter.ImportAsync(id, User.Identity?.Name ?? "admin", HttpContext.RequestAborted);
        if (result.Succeeded)
            return RedirectToPage("PublishTargets", new { editName = result.TargetName });
        StatusMessage = result.Message;
        StatusIsError = true;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRefreshMetadataAsync(int id)
    {
        try
        {
            await packageSourceService.RefreshMetadataAsync(id);
            StatusMessage = "Metadata refreshed.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh metadata for package source {SourceId}", id);
            StatusMessage = $"Failed to refresh metadata: {ex.Message}";
            StatusIsError = true;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync(int id)
    {
        var source = await context.PackageSources
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id);
        if (source is null)
        {
            return NotFound();
        }

        try
        {
            var result = await connectionTester.TestAsync(source, HttpContext.RequestAborted);
            await packageSourceService.UpdateSyncStateAsync(
                source.Id,
                result.Succeeded,
                result.Succeeded ? null : result.Message,
                HttpContext.RequestAborted);

            if (result.Succeeded && source.Protocol == PackageSourceProtocol.NuGet)
            {
                await packageSourceService.RefreshMetadataAsync(source.Id, HttpContext.RequestAborted);
            }

            StatusMessage = result.Message;
            StatusIsError = !result.Succeeded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test package source {SourceId}", id);
            await packageSourceService.UpdateSyncStateAsync(
                source.Id,
                success: false,
                ex.Message,
                HttpContext.RequestAborted);
            StatusMessage = $"Connection failed: {ex.Message}";
            StatusIsError = true;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleEnabledAsync(int id, bool enable)
    {
        var source = await context.PackageSources.FirstOrDefaultAsync(s => s.Id == id);
        if (source is null) return NotFound();
        source.IsEnabled = enable;
        source.LastModifiedAt = DateTimeOffset.UtcNow;
        await packageSourceService.UpdateAsync(source);
        await events.RecordAsync(enable ? "source.enabled" : "source.disabled", source.Name, cancellationToken: HttpContext.RequestAborted);
        return RedirectToPage();
    }

    private async Task LoadSourcesAsync() =>
        Sources = await context.PackageSources
            .AsNoTracking()
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync();

    private void ValidateInput()
    {
        foreach (var error in Input.ValidateCredentials(EditId.HasValue))
            ModelState.AddModelError("Input.CredentialMode", error);
        if (!PackageSourceEcosystem.TryParse(Input.Ecosystem, out var protocol, out var surface))
        {
            ModelState.AddModelError(
                "Input.Ecosystem",
                "Choose a configured package ecosystem.");
            return;
        }

        if (protocol != PackageSourceProtocol.Oci || string.IsNullOrWhiteSpace(surface))
        {
            return;
        }

        if (!OciSurfaces.Contains(surface, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                "Input.Ecosystem",
                $"Choose a configured OCI surface ({string.Join(", ", OciSurfaces)}) or leave it blank for all OCI surfaces.");
        }
    }

    public string GetProtocolLabel(PackageSource source) => source.Protocol switch
    {
        PackageSourceProtocol.NuGet => "NuGet",
        PackageSourceProtocol.Npm => "npm",
        PackageSourceProtocol.Oci when string.Equals(source.Surface, "docker", StringComparison.OrdinalIgnoreCase) => "Docker",
        PackageSourceProtocol.Oci when string.Equals(source.Surface, "helm", StringComparison.OrdinalIgnoreCase) => "Helm",
        PackageSourceProtocol.Oci => "OCI",
        _ => source.Protocol.ToString(),
    };

    public string GetDirectionLabel(PackageSourceType type) => type switch
    {
        PackageSourceType.Upstream => "Pull from upstream",
        PackageSourceType.Downstream => "Legacy downstream entry (no pulls)",
        PackageSourceType.Both => "Upstream with legacy publish settings",
        _ => type.ToString(),
    };

    public string GetStrategyLabel(PackageSourceCachingStrategy strategy) => strategy switch
    {
        PackageSourceCachingStrategy.IndexAndCache => "Tracked + cached",
        PackageSourceCachingStrategy.CacheOnly => "Private cache",
        PackageSourceCachingStrategy.ProxyOnly => "Pass through",
        _ => strategy.ToString(),
    };

    public sealed class PackageSourceInput
    {
        [Required]
        [StringLength(128)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Url]
        public string FeedUrl { get; set; } = string.Empty;

        public PackageSourceType Type { get; set; } = PackageSourceType.Upstream;

        [Required]
        [StringLength(128)]
        public string Ecosystem { get; set; } = PackageSourceEcosystem.NuGet;

        public PackageSourceCachingStrategy CachingStrategy { get; set; } =
            PackageSourceCachingStrategy.IndexAndCache;

        public string? Username { get; set; }

        public string? Password { get; set; }

        public string? ApiKey { get; set; }

        public string CredentialMode { get; set; } = "None";

        public IEnumerable<string> ValidateCredentials(bool editing)
        {
            if (CredentialMode is not ("None" or "Keep" or "Basic" or "Token") || (CredentialMode == "Keep" && !editing))
                yield return "Choose a valid authentication method.";
            if (CredentialMode == "Basic" && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)))
                yield return "Enter both a username and a password or access token.";
            if (CredentialMode == "Token" && string.IsNullOrWhiteSpace(ApiKey))
                yield return "Enter an API key or bearer token.";
            if (CredentialMode == "Token" && Ecosystem?.StartsWith("Oci", StringComparison.OrdinalIgnoreCase) == true)
                yield return "Docker and Helm upstreams use username and password / access token authentication.";
            // Disabled fields and crafted requests must not retain incompatible secrets.
            if (CredentialMode != "Basic") { Username = null; Password = null; }
            if (CredentialMode != "Token") ApiKey = null;
        }

        public static PackageSourceInput FromEntity(PackageSource source) => new()
        {
            Name = source.Name,
            FeedUrl = source.FeedUrl,
            Type = source.Type,
            Ecosystem = PackageSourceEcosystem.FromSource(source),
            CachingStrategy = source.CachingStrategy,
            // Stored credentials remain encrypted and are never round-tripped through HTML.
            // Blank credential inputs while editing mean "keep the current value".
            Username = null,
            Password = null,
            ApiKey = null,
            CredentialMode = "Keep",
        };

        public PackageSource ToEntity() => ApplyTo(new PackageSource());

        public PackageSource ApplyTo(PackageSource source)
        {
            if (!PackageSourceEcosystem.TryParse(Ecosystem, out var protocol, out var surface))
            {
                throw new InvalidOperationException($"Unknown package ecosystem '{Ecosystem}'.");
            }

            source.Name = Name.Trim();
            source.FeedUrl = FeedUrl.Trim();
            source.Type = Type;
            source.Protocol = protocol;
            source.Surface = surface;
            source.CachingStrategy = CachingStrategy;
            source.Username = Username;
            source.Password = Password;
            source.ApiKey = ApiKey;
            return source;
        }
    }

    public sealed record EcosystemOption(string Value, string Label);
}
