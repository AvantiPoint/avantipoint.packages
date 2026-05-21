using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostAuthenticationExtensions
{
    public static IServiceCollection AddHostAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Host:Authentication").Get<HostAuthenticationOptions>()
            ?? new HostAuthenticationOptions();

        var resolvedProvider = HostAuthenticationResolver.Resolve(options);

        ValidateProviderConfiguration(options, resolvedProvider);

        var authBuilder = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.LoginPath = "/Account/Login";
                cookie.AccessDeniedPath = "/Account/AccessDenied";
            });

        switch (resolvedProvider)
        {
            case HostAuthenticationProvider.MicrosoftAccount:
                AddMicrosoftAccount(authBuilder, options.Microsoft);
                break;
            case HostAuthenticationProvider.Google:
                AddGoogle(authBuilder, options.Google);
                break;
            case HostAuthenticationProvider.GitHub:
                AddGitHub(authBuilder, options.GitHub);
                break;
        }

        services.AddAuthorization();
        return services;
    }

    private static void ValidateProviderConfiguration(
        HostAuthenticationOptions options,
        HostAuthenticationProvider provider)
    {
        switch (provider)
        {
            case HostAuthenticationProvider.MicrosoftAccount:
                if (string.IsNullOrWhiteSpace(options.Microsoft.TenantId) ||
                    IsPublicMicrosoftTenant(options.Microsoft.TenantId))
                {
                    throw new InvalidOperationException(
                        "Microsoft Account requires an organizational TenantId (not \"common\", \"consumers\", or \"organizations\").");
                }

                break;
            case HostAuthenticationProvider.Google:
                if (string.IsNullOrWhiteSpace(options.Google.HostedDomain))
                {
                    throw new InvalidOperationException(
                        "Google requires HostedDomain for your Google Workspace domain.");
                }

                break;
            case HostAuthenticationProvider.GitHub:
                if (string.IsNullOrWhiteSpace(options.GitHub.Organization))
                {
                    throw new InvalidOperationException(
                        "GitHub requires Organization.");
                }

                break;
        }
    }

    private static bool IsPublicMicrosoftTenant(string tenantId) =>
        string.Equals(tenantId, "common", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tenantId, "consumers", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tenantId, "organizations", StringComparison.OrdinalIgnoreCase);

    private static void AddMicrosoftAccount(AuthenticationBuilder authBuilder, HostMicrosoftOptions accountOptions)
    {
        authBuilder.AddMicrosoftAccount(microsoft =>
        {
            microsoft.ClientId = accountOptions.ClientId;
            microsoft.ClientSecret = accountOptions.ClientSecret;
            if (!string.IsNullOrWhiteSpace(accountOptions.TenantId) && !IsPublicMicrosoftTenant(accountOptions.TenantId))
            {
                microsoft.AuthorizationEndpoint =
                    $"https://login.microsoftonline.com/{accountOptions.TenantId}/oauth2/v2.0/authorize";
                microsoft.TokenEndpoint =
                    $"https://login.microsoftonline.com/{accountOptions.TenantId}/oauth2/v2.0/token";
            }

            microsoft.Events.OnCreatingTicket = OnOAuthCreatingTicketAsync(HostExternalAuthProvider.MicrosoftAccount);
        });
    }

    private static void AddGoogle(AuthenticationBuilder authBuilder, HostGoogleOptions googleOptions)
    {
        authBuilder.AddGoogle(google =>
        {
            google.ClientId = googleOptions.ClientId;
            google.ClientSecret = googleOptions.ClientSecret;
            google.Events.OnCreatingTicket = OnOAuthCreatingTicketAsync(HostExternalAuthProvider.Google);
        });
    }

    private static void AddGitHub(AuthenticationBuilder authBuilder, HostGitHubOptions githubOptions)
    {
        authBuilder.AddGitHub(github =>
        {
            github.ClientId = githubOptions.ClientId;
            github.ClientSecret = githubOptions.ClientSecret;
            github.Scope.Add("read:user");
            github.Scope.Add("user:email");
            github.Scope.Add("read:org");
            github.Events.OnCreatingTicket = OnOAuthCreatingTicketAsync(HostExternalAuthProvider.GitHub);
        });
    }

    private static Func<OAuthCreatingTicketContext, Task> OnOAuthCreatingTicketAsync(HostExternalAuthProvider provider) =>
        async context =>
        {
            if (!await ValidateAndProvisionAsync(
                    context.Principal!,
                    context.HttpContext,
                    provider,
                    context.Fail,
                    context.HttpContext.RequestAborted,
                    context.AccessToken))
            {
                return;
            }
        };

    private static async Task<bool> ValidateAndProvisionAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        HostExternalAuthProvider provider,
        Action<string> fail,
        CancellationToken cancellationToken,
        string? oauthAccessToken = null)
    {
        var validator = httpContext.RequestServices.GetRequiredService<IHostExternalLoginValidator>();
        var validation = await validator.ValidateAsync(principal, provider, oauthAccessToken, cancellationToken);
        if (!validation.Succeeded)
        {
            fail(validation.ErrorMessage ?? "Access denied.");
            return false;
        }

        try
        {
            var provisioner = httpContext.RequestServices.GetRequiredService<IHostUserProvisioner>();
            var user = await provisioner.ProvisionUserAsync(principal, provider, cancellationToken);
            if (user.ApprovalStatus == HostUserApprovalStatus.Pending)
            {
                fail("Account pending approval.");
                return false;
            }

            AddRoleClaims(principal, user);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            fail(ex.Message);
            return false;
        }
    }

    private static void AddRoleClaims(ClaimsPrincipal principal, HostUser user)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        if (user.IsAdmin)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, Authentication.FeedRoles.Admin));
        }

        if (user.CanPublish)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, Authentication.FeedRoles.Publisher));
        }

        if (user.CanConsume)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, Authentication.FeedRoles.Consumer));
        }
    }
}
