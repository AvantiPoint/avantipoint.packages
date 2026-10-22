using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AvantiPoint.Packages.Host.Admin.Configuration;

public static class HostPathBase
{
    public const string ConfigurationKey = "Host:PathBase";

    public static PathString Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration[ConfigurationKey]?.Trim();
        if (string.IsNullOrEmpty(configured) || configured == "/")
        {
            return PathString.Empty;
        }

        if (!configured.StartsWith('/')
            || configured.Contains('?', StringComparison.Ordinal)
            || configured.Contains('#', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be an absolute path such as '/packages'.");
        }

        return new PathString(configured.TrimEnd('/'));
    }
}
