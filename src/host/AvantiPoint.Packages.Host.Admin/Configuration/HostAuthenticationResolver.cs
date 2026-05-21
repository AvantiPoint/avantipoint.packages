using AvantiPoint.Packages.Host.Admin.Entities;

namespace AvantiPoint.Packages.Host.Admin.Configuration;

public static class HostAuthenticationResolver
{
    private static readonly HostAuthenticationProvider[] AutoDetectOrder =
    [
        HostAuthenticationProvider.MicrosoftAccount,
        HostAuthenticationProvider.Google,
        HostAuthenticationProvider.GitHub,
    ];

    /// <summary>
    /// Resolves the first fully configured provider in priority order (Microsoft, Google, GitHub).
    /// Returns null when no provider has both ClientId and ClientSecret configured (open UI for local dev).
    /// Throws when any provider section has partial credentials.
    /// </summary>
    public static HostAuthenticationProvider? TryResolve(HostAuthenticationOptions options)
    {
        ThrowIfPartiallyConfigured(options);

        foreach (var provider in AutoDetectOrder)
        {
            if (IsFullyConfigured(options, provider))
            {
                return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the active provider or throws when none is fully configured.
    /// </summary>
    public static HostAuthenticationProvider Resolve(HostAuthenticationOptions options)
    {
        var resolved = TryResolve(options);
        if (resolved is null)
        {
            throw new InvalidOperationException(
                "Host authentication is not configured. Set ClientId and ClientSecret for one of: " +
                "Host:Authentication:Microsoft, Host:Authentication:Google, or Host:Authentication:GitHub.");
        }

        return resolved.Value;
    }

    public static bool IsFullyConfigured(HostAuthenticationOptions options, HostAuthenticationProvider provider) =>
        provider switch
        {
            HostAuthenticationProvider.MicrosoftAccount =>
                !string.IsNullOrWhiteSpace(options.Microsoft.ClientId) &&
                !string.IsNullOrWhiteSpace(options.Microsoft.ClientSecret),
            HostAuthenticationProvider.Google =>
                !string.IsNullOrWhiteSpace(options.Google.ClientId) &&
                !string.IsNullOrWhiteSpace(options.Google.ClientSecret),
            HostAuthenticationProvider.GitHub =>
                !string.IsNullOrWhiteSpace(options.GitHub.ClientId) &&
                !string.IsNullOrWhiteSpace(options.GitHub.ClientSecret),
            _ => false,
        };

    public static HostExternalAuthProvider ToExternalProvider(HostAuthenticationProvider provider) =>
        provider switch
        {
            HostAuthenticationProvider.MicrosoftAccount => HostExternalAuthProvider.MicrosoftAccount,
            HostAuthenticationProvider.Google => HostExternalAuthProvider.Google,
            HostAuthenticationProvider.GitHub => HostExternalAuthProvider.GitHub,
            _ => HostExternalAuthProvider.Unknown,
        };

    private static void ThrowIfPartiallyConfigured(HostAuthenticationOptions options)
    {
        if (IsPartiallyConfigured(options.Microsoft.ClientId, options.Microsoft.ClientSecret))
        {
            throw new InvalidOperationException(
                "Host:Authentication:Microsoft is partially configured. Set both ClientId and ClientSecret, or leave both empty.");
        }

        if (IsPartiallyConfigured(options.Google.ClientId, options.Google.ClientSecret))
        {
            throw new InvalidOperationException(
                "Host:Authentication:Google is partially configured. Set both ClientId and ClientSecret, or leave both empty.");
        }

        if (IsPartiallyConfigured(options.GitHub.ClientId, options.GitHub.ClientSecret))
        {
            throw new InvalidOperationException(
                "Host:Authentication:GitHub is partially configured. Set both ClientId and ClientSecret, or leave both empty.");
        }
    }

    private static bool IsPartiallyConfigured(string clientId, string clientSecret)
    {
        var hasId = !string.IsNullOrWhiteSpace(clientId);
        var hasSecret = !string.IsNullOrWhiteSpace(clientSecret);
        return hasId != hasSecret;
    }
}
