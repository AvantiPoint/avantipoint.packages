using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Packages.UI.Services;

public static class FeedProtocolPreference
{
    public const string CookieName = "ap-feed-protocol";
    public const string NuGet = "nuget";
    public const string Npm = "npm";
    public const string Oci = "oci";

    public static string? TryInferFromPath(string? path)
    {
        var p = NormalizePath(path);
        if (IsNeutralPath(p))
        {
            return null;
        }

        if (p.Equals("/browse/npm", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/browse/npm/", StringComparison.OrdinalIgnoreCase))
        {
            return Npm;
        }

        if (p.Equals("/oci", StringComparison.OrdinalIgnoreCase))
        {
            return Oci;
        }

        if (p.Equals("/oci/connect", StringComparison.OrdinalIgnoreCase))
        {
            return Oci;
        }

        if (p.StartsWith("/oci/connect/", StringComparison.OrdinalIgnoreCase))
        {
            var segment = p["/oci/connect/".Length..];
            return IsSafeSegment(segment) ? Oci + ":" + segment : Oci;
        }

        if (p.StartsWith("/oci/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = p["/oci/".Length..];
            var slash = rest.IndexOf("/");
            var first = slash < 0 ? rest : rest[..slash];
            if (string.IsNullOrEmpty(first)
                || first.Equals("repos", StringComparison.OrdinalIgnoreCase))
            {
                return Oci;
            }

            return Oci + ":" + first;
        }

        if (p is "/" or "/feed"
            || p.StartsWith("/packages/", StringComparison.OrdinalIgnoreCase))
        {
            return NuGet;
        }

        return null;
    }

    public static bool IsNeutralPath(string? path)
    {
        var p = NormalizePath(path);
        return p.StartsWith("/account", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/auth", StringComparison.OrdinalIgnoreCase);
    }

    public static string Resolve(string? path, string? cookieValue)
    {
        var inferred = TryInferFromPath(path);
        if (inferred is not null)
        {
            return inferred;
        }

        return TryNormalizeCookie(cookieValue, out var cookie) ? cookie : NuGet;
    }

    public static string GetKind(string? protocol)
    {
        if (TryNormalizeCookie(protocol, out var value))
        {
            if (value == Npm)
            {
                return Npm;
            }

            if (value == Oci || value.StartsWith("oci:", StringComparison.OrdinalIgnoreCase))
            {
                return Oci;
            }
        }

        return NuGet;
    }

    public static string? GetOciSegment(string? protocol)
    {
        if (TryNormalizeCookie(protocol, out var value)
            && value.StartsWith("oci:", StringComparison.OrdinalIgnoreCase)
            && value.Length > 4)
        {
            return value[4..];
        }

        return null;
    }

    public static string ToHref(string? protocol)
    {
        if (!TryNormalizeCookie(protocol, out var value))
        {
            return "/";
        }

        if (value == Npm)
        {
            return "/browse/npm";
        }

        if (value == Oci)
        {
            return "/oci";
        }

        if (value.StartsWith("oci:", StringComparison.OrdinalIgnoreCase))
        {
            return "/oci/" + value[4..];
        }

        return "/";
    }

    public static string FromHref(string? href) => TryInferFromPath(href) ?? NuGet;

    public static string ConnectHref(string? protocol)
    {
        var kind = GetKind(protocol);
        return kind switch
        {
            Npm => "/browse/npm/connect",
            Oci when GetOciSegment(protocol) is { } segment => "/oci/connect/" + segment,
            Oci => "/oci/connect",
            _ => "/feed",
        };
    }

    public static void WriteCookie(
        HttpResponse response,
        string value,
        bool secure,
        PathString pathBase = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!TryNormalizeCookie(value, out var normalized))
        {
            return;
        }

        response.Cookies.Append(CookieName, normalized, new CookieOptions
        {
            Path = pathBase.HasValue ? pathBase.Value : "/",
            MaxAge = TimeSpan.FromDays(365),
            SameSite = SameSiteMode.Lax,
            HttpOnly = false,
            IsEssential = true,
            Secure = secure,
        });
    }

    public static bool TryNormalizeCookie(string? value, out string normalized)
    {
        normalized = NuGet;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals(NuGet, StringComparison.OrdinalIgnoreCase))
        {
            normalized = NuGet;
            return true;
        }

        if (trimmed.Equals(Npm, StringComparison.OrdinalIgnoreCase))
        {
            normalized = Npm;
            return true;
        }

        if (trimmed.Equals(Oci, StringComparison.OrdinalIgnoreCase))
        {
            normalized = Oci;
            return true;
        }

        if (trimmed.StartsWith("oci:", StringComparison.OrdinalIgnoreCase)
            && trimmed.Length > 4)
        {
            var segment = trimmed[4..];
            if (IsSafeSegment(segment))
            {
                normalized = Oci + ":" + segment;
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var p = path.Trim();
        var q = p.IndexOfAny(['?', '#']);
        if (q >= 0)
        {
            p = p[..q];
        }

        if (!p.StartsWith("/"))
        {
            p = "/" + p;
        }

        if (p.Length > 1 && p.EndsWith("/"))
        {
            p = p.TrimEnd("/"[0]);
        }

        return p;
    }

    private static bool IsSafeSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) || segment.Length > 64)
        {
            return false;
        }

        foreach (var c in segment)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
