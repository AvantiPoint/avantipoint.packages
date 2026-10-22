using Microsoft.AspNetCore.Components;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Builds browser-facing links relative to the application's configured base path.
/// </summary>
public static class FeedUiPath
{
    public static string ToHref(NavigationManager navigation, string applicationPath)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return ToHref(new Uri(navigation.BaseUri), applicationPath);
    }

    public static string ToHref(Uri baseUri, string applicationPath)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(applicationPath);

        if (applicationPath.StartsWith('#')
            || (!applicationPath.StartsWith('/')
                && Uri.TryCreate(applicationPath, UriKind.Absolute, out _)))
        {
            return applicationPath;
        }

        var resolved = new Uri(baseUri, applicationPath.TrimStart('/'));
        return resolved.PathAndQuery + resolved.Fragment;
    }

    public static string CurrentApplicationPath(NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        var separator = relative.IndexOfAny(['?', '#']);
        if (separator >= 0)
        {
            relative = relative[..separator];
        }

        return string.IsNullOrEmpty(relative) ? "/" : "/" + relative.TrimStart('/');
    }

    public static string CookiePath(NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        var basePath = new Uri(navigation.BaseUri).AbsolutePath.TrimEnd('/');
        return string.IsNullOrEmpty(basePath) ? "/" : basePath;
    }
}
