namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Formats registry host strings for CLI examples (docker/helm/oras), without URL scheme.
/// </summary>
internal static class RegistryCommandHost
{
    public static string GetLoginHost(string registryUrl)
    {
        if (Uri.TryCreate(registryUrl, UriKind.Absolute, out var uri))
        {
            return uri.Authority;
        }

        return registryUrl.TrimEnd('/').Replace("/v2/", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetPullHost(string registryUrl, string? segment)
    {
        var loginHost = GetLoginHost(registryUrl);
        return string.IsNullOrEmpty(segment) ? loginHost : $"{loginHost}/{segment}";
    }
}
