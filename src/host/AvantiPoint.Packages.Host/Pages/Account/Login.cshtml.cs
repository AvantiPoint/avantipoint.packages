using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using GitHubAuthenticationDefaults = AspNet.Security.OAuth.GitHub.GitHubAuthenticationDefaults;

namespace AvantiPoint.Packages.Host.Pages.Account;

[AllowAnonymous]
public class LoginModel(IOptions<HostAuthenticationOptions> authOptions) : PageModel
{
    public bool AuthConfigured { get; private set; }

    public string ProviderDisplayName { get; private set; } = string.Empty;

    public string SignInPath { get; private set; } = string.Empty;

    public void OnGet()
    {
        var resolved = HostAuthenticationResolver.TryResolve(authOptions.Value);
        if (resolved is null)
        {
            AuthConfigured = false;
            return;
        }

        AuthConfigured = true;
        (ProviderDisplayName, SignInPath) = resolved.Value switch
        {
            HostAuthenticationProvider.MicrosoftAccount => ("Microsoft", "/signin-microsoft"),
            HostAuthenticationProvider.Google => ("Google", "/signin-google"),
            HostAuthenticationProvider.GitHub => ("GitHub", "/signin-github"),
            _ => (string.Empty, string.Empty),
        };
    }

    public IActionResult OnPost(string? returnUrl = null)
    {
        var resolved = HostAuthenticationResolver.TryResolve(authOptions.Value);
        if (resolved is null)
        {
            return Page();
        }

        var scheme = resolved.Value switch
        {
            HostAuthenticationProvider.MicrosoftAccount => MicrosoftAccountDefaults.AuthenticationScheme,
            HostAuthenticationProvider.Google => GoogleDefaults.AuthenticationScheme,
            HostAuthenticationProvider.GitHub => GitHubAuthenticationDefaults.AuthenticationScheme,
            _ => string.Empty,
        };

        var properties = new AuthenticationProperties
        {
            RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl,
        };

        return Challenge(properties, scheme);
    }
}
