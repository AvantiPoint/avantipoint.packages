using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed class HostExternalLoginValidator(
    IOptions<HostAuthenticationOptions> authOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<HostExternalLoginValidator> logger) : IHostExternalLoginValidator
{
    private static readonly HashSet<string> PublicMicrosoftTenantIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "common",
        "consumers",
        "organizations",
    };

    public async Task<HostLoginValidationResult> ValidateAsync(
        ClaimsPrincipal principal,
        HostExternalAuthProvider provider,
        string? oauthAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        var options = authOptions.Value;

        return provider switch
        {
            HostExternalAuthProvider.MicrosoftAccount => ValidateMicrosoftAccount(principal, options.Microsoft),
            HostExternalAuthProvider.Google => ValidateGoogle(principal, options.Google),
            HostExternalAuthProvider.GitHub => await ValidateGitHubAsync(
                principal,
                options.GitHub,
                oauthAccessToken,
                cancellationToken),
            _ => HostLoginValidationResult.Fail("Unsupported authentication provider."),
        };
    }

    private static HostLoginValidationResult ValidateMicrosoftAccount(
        ClaimsPrincipal principal,
        HostMicrosoftOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TenantId) ||
            PublicMicrosoftTenantIds.Contains(options.TenantId))
        {
            return HostLoginValidationResult.Fail(
                "Microsoft Account sign-in must target an organizational tenant. Set Host:Authentication:Microsoft:TenantId to your directory tenant ID (not \"common\").");
        }

        var tokenTenant = principal.FindFirstValue("tid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid");
        if (!string.IsNullOrWhiteSpace(tokenTenant) &&
            !string.Equals(tokenTenant, options.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return HostLoginValidationResult.Fail(
                "You must sign in with an account from the configured organizational tenant.");
        }

        var email = GetEmail(principal);
        if (options.AllowedEmailDomains.Count > 0)
        {
            var domain = GetEmailDomain(email);
            if (domain is null || !options.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                return HostLoginValidationResult.Fail(
                    "Your email domain is not authorized for this feed.");
            }

            return ValidateMicrosoftAccountRequiredGroups(principal, options);
        }

        return ValidateMicrosoftAccountRequiredGroups(principal, options);
    }

    private static HostLoginValidationResult ValidateGoogle(
        ClaimsPrincipal principal,
        HostGoogleOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HostedDomain))
        {
            return HostLoginValidationResult.Fail(
                "Google sign-in requires Host:Authentication:Google:HostedDomain for your Google Workspace domain.");
        }

        var hostedDomain = principal.FindFirstValue("hd");
        if (!string.Equals(hostedDomain, options.HostedDomain, StringComparison.OrdinalIgnoreCase))
        {
            return HostLoginValidationResult.Fail(
                $"You must sign in with a Google Workspace account from the {options.HostedDomain} domain.");
        }

        return ValidateGoogleRequiredGroups(options);
    }

    // Google OAuth ID tokens do not include group membership; RequiredGroupIds enforcement
    // requires Admin SDK Directory API or Cloud Identity Groups API (phase 2).
    private static HostLoginValidationResult ValidateGoogleRequiredGroups(HostGoogleOptions options)
    {
        if (options.RequiredGroupIds.Count == 0)
        {
            return HostLoginValidationResult.Success();
        }

        return HostLoginValidationResult.Fail(
            "Google Workspace group membership is configured but not yet enforced. " +
            "Remove Host:Authentication:Google:RequiredGroupIds or wait for Admin SDK integration.");
    }

    private async Task<HostLoginValidationResult> ValidateGitHubAsync(
        ClaimsPrincipal principal,
        HostGitHubOptions options,
        string? oauthAccessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Organization))
        {
            return HostLoginValidationResult.Fail(
                "GitHub sign-in requires Host:Authentication:GitHub:Organization.");
        }

        if (string.IsNullOrWhiteSpace(oauthAccessToken))
        {
            return HostLoginValidationResult.Fail("GitHub access token was not available for organization verification.");
        }

        var login = GetGitHubLogin(principal);
        if (string.IsNullOrWhiteSpace(login))
        {
            return HostLoginValidationResult.Fail("GitHub username could not be determined.");
        }

        var client = httpClientFactory.CreateClient(nameof(HostExternalLoginValidator));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauthAccessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AvantiPoint-Packages-Host", "1.0"));

        var orgMemberUrl = $"https://api.github.com/orgs/{Uri.EscapeDataString(options.Organization)}/members/{Uri.EscapeDataString(login)}";
        using var memberResponse = await client.GetAsync(orgMemberUrl, cancellationToken);
        if (memberResponse.StatusCode != HttpStatusCode.NoContent && memberResponse.StatusCode != HttpStatusCode.OK)
        {
            logger.LogInformation(
                "GitHub org membership check failed for {Login} in {Org}: {Status}",
                login,
                options.Organization,
                memberResponse.StatusCode);
            return HostLoginValidationResult.Fail(
                $"You must be a member of the {options.Organization} GitHub organization.");
        }

        if (options.TeamSlugs.Count == 0)
        {
            return HostLoginValidationResult.Success();
        }

        foreach (var teamSlug in options.TeamSlugs)
        {
            if (string.IsNullOrWhiteSpace(teamSlug))
            {
                continue;
            }

            var teamUrl =
                $"https://api.github.com/orgs/{Uri.EscapeDataString(options.Organization)}/teams/{Uri.EscapeDataString(teamSlug)}/members/{Uri.EscapeDataString(login)}";
            using var teamResponse = await client.GetAsync(teamUrl, cancellationToken);
            if (teamResponse.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK)
            {
                return HostLoginValidationResult.Success();
            }
        }

        return HostLoginValidationResult.Fail(
            $"You must belong to at least one authorized team in the {options.Organization} organization.");
    }

    // Microsoft Account OAuth tokens often omit "groups" claims unless the app has GroupMember.Read.All
    // and admin consent; configure RequiredGroupIds only when those claims are available.
    private static HostLoginValidationResult ValidateMicrosoftAccountRequiredGroups(
        ClaimsPrincipal principal,
        HostMicrosoftOptions options)
    {
        if (options.RequiredGroupIds.Count == 0)
        {
            return HostLoginValidationResult.Success();
        }

        var groups = principal.FindAll("groups").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (options.RequiredGroupIds.Any(id => groups.Contains(id)))
        {
            return HostLoginValidationResult.Success();
        }

        return HostLoginValidationResult.Fail(
            "You must be a member of a required security group to access this feed.");
    }

    private static string? GetEmail(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email)
        ?? principal.FindFirstValue("preferred_username");

    private static string? GetEmailDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var at = email.LastIndexOf('@');
        return at < 0 || at >= email.Length - 1 ? null : email[(at + 1)..];
    }

    private static string? GetGitHubLogin(ClaimsPrincipal principal) =>
        principal.FindFirstValue("urn:github:login")
        ?? principal.FindFirstValue(ClaimTypes.Name);
}
