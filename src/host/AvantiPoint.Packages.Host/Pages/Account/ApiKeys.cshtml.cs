using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.UI.Services;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Consumer)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ApiKeysModel(
    IHostIdentityContext db,
    HostApiTokenService tokenService,
    IFeedRegistry feedRegistry,
    IPublicBaseUrlProvider publicBaseUrlProvider) : PageModel
{
    public IList<HostApiToken> Tokens { get; private set; } = [];

    public string UsernameEmail { get; private set; } = string.Empty;

    public string ProtocolKind { get; private set; } = FeedProtocolPreference.NuGet;

    public string FeedUrl { get; private set; } = string.Empty;

    public string NpmAuthTokenLine { get; private set; } = string.Empty;

    public string OciLoginHost { get; private set; } = string.Empty;

    public bool CanCreateWriteToken { get; private set; }

    [BindProperty]
    public string? NewDescription { get; set; }

    [BindProperty]
    public bool NewScopeRead { get; set; } = true;

    [BindProperty]
    public bool NewScopeWrite { get; set; }

    [TempData]
    public string? LastCreatedToken { get; set; }

    [BindProperty]
    public bool NewAllFeeds { get; set; }

    [BindProperty]
    public List<string> NewFeedIds { get; set; } = [];

    [BindProperty]
    public int NewExpirationDays { get; set; } = 90;

    public IReadOnlyList<SurfaceRegistration> AvailableFeeds => feedRegistry.Surfaces;

    public string GetFeedLabel(SurfaceRegistration surface) => surface.Protocol switch
    {
        FeedProtocol.NuGet => "NuGet",
        FeedProtocol.Npm => "npm",
        _ when string.Equals(surface.OciSegment, "helm", StringComparison.OrdinalIgnoreCase) => "Helm",
        _ when string.Equals(surface.OciSegment, "docker", StringComparison.OrdinalIgnoreCase) => "Docker",
        _ => string.IsNullOrEmpty(surface.OciSegment) ? "OCI (default endpoint)" : $"OCI: {surface.OciSegment}",
    };

    public string GetFeedScopeLabel(HostApiToken token) => token.AllFeeds
        ? "All feeds (including future feeds)"
        : string.Join(", ", token.Feeds.Select(grant =>
            feedRegistry.Surfaces.FirstOrDefault(surface => surface.SurfaceId == grant.SurfaceId) is { } surface
                ? GetFeedLabel(surface) : grant.SurfaceId));

    public async Task OnGetAsync()
    {
        BindUserContext();
        await LoadTokensAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        BindUserContext();
        var email = UsernameEmail;
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, "Your account email is required to create a token.");
            await LoadTokensAsync();
            return Page();
        }

        if (!ModelState.IsValid)
        {
            await LoadTokensAsync();
            return Page();
        }

        try
        {
            LastCreatedToken = await tokenService.CreateAsync(email, NewDescription, SelectedScopes(),
                NewAllFeeds, NewFeedIds, NewExpirationDays, HttpContext.RequestAborted);
            // TempData is data-protected and consumed by the next GET. Redirecting
            // prevents refresh/back from replaying the token-creation POST.
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        await LoadTokensAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(int id)
    {
        BindUserContext();
        var email = UsernameEmail;
        if (!await tokenService.RevokeAsync(email, id, HttpContext.RequestAborted)) return NotFound();
        return RedirectToPage();
    }

    private FeedTokenScope SelectedScopes()
    {
        var scopes = FeedTokenScope.None;
        if (NewScopeRead)
        {
            scopes |= FeedTokenScope.Read;
        }

        if (NewScopeWrite)
        {
            scopes |= FeedTokenScope.Write;
        }

        return scopes;
    }

    private void BindUserContext()
    {
        UsernameEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        BindProtocolSnippets();
    }

    private async Task LoadTokensAsync()
    {
        var email = UsernameEmail;
        if (string.IsNullOrEmpty(email))
        {
            Tokens = [];
            CanCreateWriteToken = false;
            return;
        }

        var user = await db.HostUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Email == email);
        CanCreateWriteToken = user is
        {
            CanPublish: true,
            IsRevoked: false,
            ApprovalStatus: HostUserApprovalStatus.Approved,
        };

        var tokens = await db.HostApiTokens
            .AsNoTracking()
            .Include(t => t.Feeds)
            .Where(t => t.UserEmail == email && !t.IsSystemToken && !t.Revoked)
            .ToListAsync();
        // SQLite cannot ORDER BY DateTimeOffset; sort after materialize.
        var now = DateTimeOffset.UtcNow;
        Tokens = tokens
            .Where(t => t.Expires > now)
            .OrderByDescending(t => t.Created)
            .ToList();
    }

    private void BindProtocolSnippets()
    {
        var protocol = FeedProtocolPreference.Resolve(Request.Path.Value, Request.Cookies[FeedProtocolPreference.CookieName]);
        ProtocolKind = FeedProtocolPreference.GetKind(protocol);
        if (ProtocolKind == FeedProtocolPreference.Npm)
        {
            BindAlt();
            return;
        }

        if (ProtocolKind == FeedProtocolPreference.Oci)
        {
            BindOci(protocol);
            return;
        }

        BindNuGet();
    }

    private void BindNuGet()
    {
        var surface = feedRegistry.TryGetNuGetSurface();
        FeedUrl = publicBaseUrlProvider
            .GetSurfacePublicBaseUrl(HttpContext, surface?.RoutePrefix ?? string.Empty)
            .ToString()
            .TrimEnd('/') + "/v3/index.json";
        NpmAuthTokenLine = string.Empty;
        OciLoginHost = string.Empty;
    }

    private void BindAlt()
    {
        BindAltUrl();
        BindAltAuth();
        OciLoginHost = string.Empty;
    }

    private void BindAltUrl()
    {
        var surface = feedRegistry.TryGetNpmSurface();
        if (surface is not null)
        {
            FeedUrl = publicBaseUrlProvider.GetSurfacePublicBaseUrl(HttpContext, surface.RoutePrefix).ToString().TrimEnd('/') + "/";
            return;
        }

        FeedUrl = publicBaseUrlProvider.GetRequestOrigin(HttpContext).ToString().TrimEnd('/') + "/npm/";
    }

    private void BindAltAuth()
    {
        if (Uri.TryCreate(FeedUrl, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.TrimEnd('/');
            NpmAuthTokenLine = $"//{uri.Authority}{path}:_authToken=";
            return;
        }

        NpmAuthTokenLine = "//localhost/npm/:_authToken=";
    }

    private void BindOci(string protocol)
    {
        var segment = FeedProtocolPreference.GetOciSegment(protocol);
        var surface = string.IsNullOrEmpty(segment)
            ? feedRegistry.TryGetDefaultOciSurface()
            : feedRegistry.TryGetOciSurfaceBySegment(segment);
        BindOciUrl(surface);
        NpmAuthTokenLine = string.Empty;
    }

    private void BindOciUrl(SurfaceRegistration? surface)
    {
        if (surface is null)
        {
            FeedUrl = publicBaseUrlProvider.GetRequestOrigin(HttpContext).ToString().TrimEnd('/') + "/v2/";
        }
        else
        {
            FeedUrl = publicBaseUrlProvider.GetSurfacePublicBaseUrl(HttpContext, surface.RoutePrefix).ToString().TrimEnd('/') + "/v2/";
        }

        OciLoginHost = Uri.TryCreate(FeedUrl, UriKind.Absolute, out var uri)
            ? uri.Authority
            : (Request.Host.Value ?? "localhost");
    }

    public string NuGetCli(string secret) =>
        NuGetCommandBuilder.BuildAddSource(FeedUrl, "AvantiPoint", UsernameEmail, secret);

    public string OciTokenPrompt => OciCommandBuilder.BuildMaskedTokenPrompt();

    public string OciCmd(string command) => OciCommandBuilder.BuildLogin(
        command,
        OciLoginHost,
        string.IsNullOrWhiteSpace(UsernameEmail) ? "<email>" : UsernameEmail,
        Uri.TryCreate(FeedUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp);

    public string NpmrcLine(string secret) => NpmAuthTokenLine + secret;

    public string SecretPlaceholder { get; } = "<token>";
}
