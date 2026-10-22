namespace AvantiPoint.Packages.UI.Services;

public static class NpmRegistryInstructions
{
    public static string BuildAuthTokenLine(string registryUrl)
    {
        if (Uri.TryCreate(registryUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var path = uri.AbsolutePath.Trim('/');
            var registryPath = string.IsNullOrEmpty(path) ? "/" : $"/{path}/";
            return $"//{uri.Authority}{registryPath}:_authToken=${{NPM_TOKEN}}";
        }

        var relativePath = registryUrl.Trim().Trim('/');
        var fallbackPath = string.IsNullOrEmpty(relativePath) ? "/npm/" : $"/{relativePath}/";
        return $"//localhost{fallbackPath}:_authToken=${{NPM_TOKEN}}";
    }
}
