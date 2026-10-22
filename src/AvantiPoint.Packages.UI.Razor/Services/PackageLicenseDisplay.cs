using NuGet.Packaging.Licenses;

namespace AvantiPoint.Packages.UI.Services;

internal static class PackageLicenseDisplay
{
    internal static (string Text, string? Url) Describe(string? expression, bool embedded, string? licenseUrl)
    {
        // A bundled file takes precedence over legacy or malformed expression metadata.
        if (embedded) return ("Custom license", null);
        if (!string.IsNullOrWhiteSpace(expression))
        {
            try
            {
                var parsed = NuGetLicenseExpression.Parse(expression.Trim());
                if (parsed is NuGetLicense license && license.IsStandardLicense && !license.Plus)
                    return (license.Identifier, $"https://spdx.org/licenses/{Uri.EscapeDataString(license.Identifier)}.html");
                if (parsed.HasOnlyStandardIdentifiers())
                    return (expression.Trim(), $"https://licenses.nuget.org/{Uri.EscapeDataString(expression.Trim())}");
            }
            catch (NuGetLicenseExpressionParsingException) { }
            return ("Custom license", null);
        }
        return Uri.TryCreate(licenseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? ("License", uri.ToString()) : (string.Empty, null);
    }
}
