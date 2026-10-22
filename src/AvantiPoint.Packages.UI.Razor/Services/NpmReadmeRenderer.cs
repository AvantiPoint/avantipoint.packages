using System.Net;
using System.Text.RegularExpressions;
using Markdig;

namespace AvantiPoint.Packages.UI.Services;

public static class NpmReadmeRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private static readonly Regex UrlAttributePattern = new(
        @"(?<prefix>\s(?<name>href|src)\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string Render(
        string markdown,
        string? repositoryUrl = null,
        string? repositoryDirectory = null)
    {
        var html = Markdown.ToHtml(markdown, Pipeline);
        var repository = RepositoryLinkBases.TryCreate(repositoryUrl, repositoryDirectory);
        return UrlAttributePattern.Replace(html, match => SanitizeUrlAttribute(match, repository));
    }

    private static string SanitizeUrlAttribute(Match match, RepositoryLinkBases? repository)
    {
        var attributeName = match.Groups["name"].Value;
        var encodedValue = match.Groups["value"].Value;
        var value = WebUtility.HtmlDecode(encodedValue).Trim();
        if (IsSafeUrl(attributeName, value))
        {
            if (repository?.Rewrite(attributeName, value) is { } rewritten)
            {
                return $"{match.Groups["prefix"].Value}{WebUtility.HtmlEncode(rewritten)}{match.Groups["suffix"].Value}";
            }

            return match.Value;
        }

        return attributeName.Equals("href", StringComparison.OrdinalIgnoreCase)
            ? $"{match.Groups["prefix"].Value}#{match.Groups["suffix"].Value}"
            : string.Empty;
    }

    private static bool IsSafeUrl(string attributeName, string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.StartsWith('#')
            || value.StartsWith('/')
            || value.StartsWith("./", StringComparison.Ordinal)
            || value.StartsWith("../", StringComparison.Ordinal)
            || value.StartsWith('?'))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri))
        {
            return false;
        }

        if (!uri.IsAbsoluteUri)
        {
            // Plain relative README links such as docs/getting-started.md are safe.
            return !value.Contains(':', StringComparison.Ordinal);
        }

        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        return attributeName.Equals("href", StringComparison.OrdinalIgnoreCase)
            && uri.Scheme == Uri.UriSchemeMailto;
    }

    private sealed record RepositoryLinkBases(Uri LinkBase, Uri AssetBase, Uri RootLinkBase, Uri RootAssetBase)
    {
        public static RepositoryLinkBases? TryCreate(string? repositoryUrl, string? repositoryDirectory)
        {
            var candidate = repositoryUrl?.Trim();
            if (candidate?.StartsWith("git+", StringComparison.OrdinalIgnoreCase) == true)
            {
                candidate = candidate[4..];
            }
            else if (candidate?.StartsWith("git://", StringComparison.OrdinalIgnoreCase) == true)
            {
                candidate = $"https://{candidate[6..]}";
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var repository)
                || (repository.Scheme != Uri.UriSchemeHttp && repository.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            var repositoryRoot = repository.GetLeftPart(UriPartial.Path).TrimEnd('/');
            if (repositoryRoot.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                repositoryRoot = repositoryRoot[..^4];
            }

            var directory = NormalizeDirectory(repositoryDirectory);
            var host = repository.Host;
            string rootLink;
            string rootAsset;
            if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                rootLink = $"{repositoryRoot}/blob/HEAD/";
                rootAsset = $"https://raw.githubusercontent.com{repository.AbsolutePath.TrimEnd('/').TrimEndGitSuffix()}/HEAD/";
            }
            else if (host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase))
            {
                rootLink = $"{repositoryRoot}/-/blob/HEAD/";
                rootAsset = $"{repositoryRoot}/-/raw/HEAD/";
            }
            else if (host.Equals("bitbucket.org", StringComparison.OrdinalIgnoreCase))
            {
                rootLink = $"{repositoryRoot}/src/HEAD/";
                rootAsset = $"{repositoryRoot}/raw/HEAD/";
            }
            else
            {
                rootLink = $"{repositoryRoot}/";
                rootAsset = rootLink;
            }

            return new RepositoryLinkBases(
                new Uri(rootLink + directory),
                new Uri(rootAsset + directory),
                new Uri(rootLink),
                new Uri(rootAsset));
        }

        public string? Rewrite(string attributeName, string value)
        {
            if (value.StartsWith('#')
                || value.StartsWith('?')
                || value.StartsWith("//", StringComparison.Ordinal)
                || (!value.StartsWith('/') && Uri.TryCreate(value, UriKind.Absolute, out _)))
            {
                return null;
            }

            var rootRelative = value.StartsWith('/');
            var baseUri = attributeName.Equals("src", StringComparison.OrdinalIgnoreCase)
                ? rootRelative ? RootAssetBase : AssetBase
                : rootRelative ? RootLinkBase : LinkBase;
            return new Uri(baseUri, value.TrimStart('/')).ToString();
        }

        private static string NormalizeDirectory(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var segments = value
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static segment => segment is not "." and not "..")
                .Select(Uri.EscapeDataString);
            var path = string.Join('/', segments);
            return string.IsNullOrEmpty(path) ? string.Empty : path + "/";
        }
    }

    private static string TrimEndGitSuffix(this string value) =>
        value.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
}
