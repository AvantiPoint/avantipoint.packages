using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using AspNet.Security.OAuth.GitHub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostAuthenticationExtensions
{
    public static IServiceCollection AddHostAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Host:Authentication").Get<HostAuthenticationOptions>()
            ?? new HostAuthenticationOptions();

        if (options.Providers.Count == 0)
        {
            return services;
        }

        var authBuilder = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.LoginPath = "/Account/Login";
                cookie.AccessDeniedPath = "/Account/AccessDenied";
            });

        foreach (var provider in options.Providers)
        {
            switch (provider.ToLowerInvariant())
            {
                case "microsoftentra":
                case "azuread":
                    AddMicrosoftEntra(authBuilder, configuration, options.MicrosoftEntra, services);
                    break;
                case "microsoftaccount":
                    AddMicrosoftAccount(authBuilder, options.MicrosoftAccount);
                    break;
                case "google":
                    AddGoogle(authBuilder, options.Google);
                    break;
                case "github":
                    AddGitHub(authBuilder, options.GitHub);
                    break;
            }
        }

        services.AddAuthorization();
        return services;
    }

    private static void AddMicrosoftEntra(
        AuthenticationBuilder authBuilder,
        IConfiguration configuration,
        MicrosoftEntraOptions entra,
        IServiceCollection services)
    {
        authBuilder.AddMicrosoftIdentityWebApp(configuration.GetSection("Host:Authentication:MicrosoftEntra"))
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();

        services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new OpenIdConnectEvents();
            options.Events.OnTokenValidated = async context =>
            {
                var provisioner = context.HttpContext.RequestServices.GetRequiredService<IHostUserProvisioner>();
                var user = await provisioner.ProvisionUserAsync(
                    context.Principal!,
                    HostExternalAuthProvider.MicrosoftEntra,
                    context.HttpContext.RequestAborted);

                if (user.ApprovalStatus == HostUserApprovalStatus.Pending)
                {
                    context.Fail("Account pending approval.");
                    return;
                }

                AddRoleClaims(context.Principal!, user);
            };
        });
    }

    private static void AddMicrosoftAccount(AuthenticationBuilder authBuilder, HostMicrosoftAccountOptions options)
    {
        authBuilder.AddMicrosoftAccount(microsoft =>
        {
            microsoft.ClientId = options.ClientId;
            microsoft.ClientSecret = options.ClientSecret;
            if (!string.IsNullOrWhiteSpace(options.TenantId) && options.TenantId != "common")
            {
                microsoft.AuthorizationEndpoint = $"https://login.microsoftonline.com/{options.TenantId}/oauth2/v2.0/authorize";
                microsoft.TokenEndpoint = $"https://login.microsoftonline.com/{options.TenantId}/oauth2/v2.0/token";
            }

            microsoft.Events.OnCreatingTicket = async context =>
            {
                await ProvisionOAuthUserAsync(context.Principal!, context.HttpContext, HostExternalAuthProvider.MicrosoftAccount);
            };
        });
    }

    private static void AddGoogle(AuthenticationBuilder authBuilder, HostGoogleOptions options)
    {
        authBuilder.AddGoogle(google =>
        {
            google.ClientId = options.ClientId;
            google.ClientSecret = options.ClientSecret;
            if (!string.IsNullOrWhiteSpace(options.HostedDomain))
            {
                google.Events.OnCreatingTicket = context =>
                {
                    var hostedDomain = context.Principal?.FindFirstValue("hd");
                    if (hostedDomain != options.HostedDomain)
                    {
                        context.Fail("Invalid hosted domain.");
                    }

                    return Task.CompletedTask;
                };
            }

            google.Events.OnCreatingTicket = async context =>
            {
                await ProvisionOAuthUserAsync(context.Principal!, context.HttpContext, HostExternalAuthProvider.Google);
            };
        });
    }

    private static void AddGitHub(AuthenticationBuilder authBuilder, HostGitHubOptions options)
    {
        authBuilder.AddGitHub(github =>
        {
            github.ClientId = options.ClientId;
            github.ClientSecret = options.ClientSecret;
            github.Scope.Add("read:user");
            github.Scope.Add("user:email");
            github.Events.OnCreatingTicket = async context =>
            {
                await ProvisionOAuthUserAsync(context.Principal!, context.HttpContext, HostExternalAuthProvider.GitHub);
            };
        });
    }

    private static async Task ProvisionOAuthUserAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        HostExternalAuthProvider provider)
    {
        var provisioner = httpContext.RequestServices.GetRequiredService<IHostUserProvisioner>();
        var user = await provisioner.ProvisionUserAsync(principal, provider, httpContext.RequestAborted);
        if (user.ApprovalStatus == HostUserApprovalStatus.Pending)
        {
            throw new UnauthorizedAccessException("Account pending approval.");
        }

        AddRoleClaims(principal, user);
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
