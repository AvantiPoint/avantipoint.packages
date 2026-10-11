using System.Net.Http.Headers;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Native.Maven;
using AvantiPoint.Packages.Registry.Native.Pub;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>Reads committed native metadata with explicit surface and request authorization.</summary>
public sealed class NativePackageBrowseService(
    IContext context,
    IFeedRegistry registry,
    IHttpContextAccessor httpAccessor,
    IPublicBaseUrlProvider urls,
    IOptions<FeedOptions> options,
    IFeedActionHandler? handler = null,
    IFeedTokenAuthenticationService? authentication = null,
    IOptions<NativePackageBrowseOptions>? browseOptions = null)
{
    public SurfaceContext? GetSurface(string protocol)
    {
        var kind = protocol switch
        {
            "maven" => FeedProtocol.Maven,
            "swift" => FeedProtocol.Swift,
            "pub" => FeedProtocol.Pub,
            _ => (FeedProtocol?)null,
        };
        var registration = registry.Surfaces.SingleOrDefault(surface => surface.Protocol == kind);
        var http = httpAccessor.HttpContext;
        return registration is null || http is null ? null : new SurfaceContext(
            registry.Feed.FeedId, registration.Protocol, registration.SurfaceId, registration.OciSegment,
            registration.RoutePrefix, urls.GetSurfacePublicBaseUrl(http, registration.RoutePrefix));
    }

    public async Task<int> AuthorizeAsync(string protocol, CancellationToken ct = default)
    {
        var surface = GetSurface(protocol);
        if (surface is null) return StatusCodes.Status404NotFound;
        var http = httpAccessor.HttpContext!;
        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers.Vary = "Authorization, Cookie";
        if (options.Value.Authentication.AllowAnonymousPull)
            return StatusCodes.Status200OK;
        // A browser session proves identity, not permission to read packages.
        // Feed credentials still get their operation-aware validation below.
        if (http.User.Identity?.IsAuthenticated == true && !http.Request.Headers.ContainsKey("Authorization"))
        {
            var role = browseOptions?.Value.AuthenticatedReadRole;
            return role is not null && http.User.IsInRole(role) ? 200 : 403;
        }
        var status = StatusCodes.Status401Unauthorized;
        if (authentication is not null && TryCredentials(http, surface.Protocol, out var token, out var username))
        {
            var result = await authentication.AuthenticateTokenAsync(token, FeedOperation.Pull, username, ct);
            if (result.Succeeded)
            {
                if (result.User is not null) http.User = result.User;
                return StatusCodes.Status200OK;
            }
            status = result.FailureStatusCode == 403 ? 403 : 401;
            if (result.ResponseHeaders is not null)
                foreach (var (name, value) in result.ResponseHeaders) http.Response.Headers[name] = value;
        }
        if (!http.Response.Headers.ContainsKey("WWW-Authenticate"))
            http.Response.Headers.WWWAuthenticate = surface.Protocol == FeedProtocol.Pub
                ? "Bearer realm=\"pub\"" : "Basic realm=\"AvantiPoint Packages\", charset=\"UTF-8\"";
        return status;
    }

    public const int PageSize = 50;

    public async Task<IReadOnlyList<NativePackageDetail>> SearchAsync(
        string protocol, string? query = null, CancellationToken ct = default) =>
        (await SearchPageAsync(protocol, query, 1, ct)).Packages;

    public async Task<NativePackagePage> SearchPageAsync(
        string protocol, string? query, int page, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 1000000);
        if (await AuthorizeAsync(protocol, ct) != 200) return new([], page, false);
        var surface = GetSurface(protocol)!;
        var rows = QueryArtifacts(surface);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            rows = rows.Where(artifact => artifact.PackageName.ToLower().Contains(term));
        }
        // Page identities before loading artifact rows. Include isolation keys in
        // the projection so case-insensitive provider collations cannot alias them.
        var identities = await rows.Select(artifact => new { artifact.FeedId, artifact.Protocol, artifact.PackageName })
            .Distinct().OrderBy(identity => identity.PackageName).ThenBy(identity => identity.FeedId)
            .ThenBy(identity => identity.Protocol).Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync(ct);
        var names = identities.Take(PageSize)
            .Where(identity => identity.FeedId == surface.FeedId && identity.Protocol == surface.Protocol.ToString())
            .Select(identity => identity.PackageName).ToArray();
        // Browse never needs pubspec contents. Detail queries load only one package.
        var selected = rows.Where(artifact => names.Contains(artifact.PackageName)).Select(artifact => new NativeArtifact
        {
            FeedId = artifact.FeedId, Protocol = artifact.Protocol, PackageName = artifact.PackageName,
            Version = artifact.Version, Path = artifact.Path, ContentHash = artifact.ContentHash,
            Length = artifact.Length, ContentType = artifact.ContentType, PublishedUtc = artifact.PublishedUtc,
        });
        var visible = await ReadVisibleAsync(surface, selected, ct, exactPackages: names);
        var packages = MakePackages(surface, visible);
        return new(packages, page, identities.Count > PageSize);
    }

    public async Task<NativePackageDetail?> GetPackageAsync(string protocol, string name, CancellationToken ct = default)
    {
        if (await AuthorizeAsync(protocol, ct) != 200) return null;
        var surface = GetSurface(protocol)!;
        var rows = await ReadVisibleAsync(surface,
            QueryArtifacts(surface).Where(artifact => artifact.PackageName == name), ct, name);
        return MakePackages(surface, rows).SingleOrDefault();
    }

    private IQueryable<NativeArtifact> QueryArtifacts(SurfaceContext surface)
    {
        var kind = surface.Protocol.ToString();
        return context.NativeArtifacts.AsNoTracking().Where(artifact =>
            artifact.FeedId == surface.FeedId && artifact.Protocol == kind && artifact.Version != null);
    }

    private async Task<IReadOnlyList<NativeArtifact>> ReadVisibleAsync(
        SurfaceContext surface, IQueryable<NativeArtifact> query, CancellationToken ct,
        string? exactPackage = null, string[]? exactPackages = null)
    {
        var rows = await query.ToListAsync(ct);
        var visible = new List<NativeArtifact>();
        foreach (var artifact in rows.Where(artifact => artifact.FeedId == surface.FeedId
            && artifact.Protocol == surface.Protocol.ToString() && (exactPackage is null || artifact.PackageName == exactPackage)
            && (exactPackages is null || exactPackages.Contains(artifact.PackageName, StringComparer.Ordinal))))
        {
            if (handler is null || await handler.CanAccessArtifact(
                new(surface, artifact.PackageName, artifact.Version, artifact.Path), ct))
                visible.Add(artifact);
        }
        return visible;
    }

    private static IReadOnlyList<NativePackageDetail> MakePackages(SurfaceContext surface, IEnumerable<NativeArtifact> artifacts) =>
        artifacts.GroupBy(artifact => artifact.PackageName, StringComparer.Ordinal)
            .Select(package => new NativePackageDetail(package.Key, SelectVersions(surface.Protocol, package)))
            .Where(package => package.Versions.Count > 0)
            .OrderBy(package => package.Name, StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<NativePackageVersion> SelectVersions(FeedProtocol protocol, IEnumerable<NativeArtifact> artifacts)
    {
        IComparer<string> comparer = protocol switch
        {
            FeedProtocol.Maven => MavenVersionComparer.Instance,
            FeedProtocol.Pub => PubVersionComparer.Instance,
            _ => Comparer<string>.Create((left, right) => NuGetVersion.Parse(left).CompareTo(NuGetVersion.Parse(right))),
        };
        return artifacts.GroupBy(artifact => artifact.Version, StringComparer.Ordinal)
            .Where(version => protocol != FeedProtocol.Maven || version.Any(IsCanonicalPom))
            .OrderByDescending(version => version.Key, comparer)
            .ThenByDescending(version => version.Key, StringComparer.Ordinal)
            .Select(version => new NativePackageVersion(version.Key,
                version.OrderBy(artifact => artifact.Path, StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    private static bool IsCanonicalPom(NativeArtifact artifact)
    {
        var separator = artifact.PackageName.IndexOf(':');
        if (separator <= 0) return false;
        var group = artifact.PackageName[..separator].Replace('.', '/');
        var name = artifact.PackageName[(separator + 1)..];
        return artifact.Path == $"{group}/{name}/{artifact.Version}/{name}-{artifact.Version}.pom";
    }

    private static bool TryCredentials(HttpContext http, FeedProtocol protocol, out string token, out string? username)
    {
        token = string.Empty;
        username = null;
        if (!AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization, out var header)) return false;
        if (header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            token = header.Parameter ?? string.Empty;
            return token.Length > 0;
        }
        if (protocol == FeedProtocol.Pub || !header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var credentials = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(header.Parameter ?? string.Empty));
            var separator = credentials.IndexOf(':');
            if (separator <= 0 || separator == credentials.Length - 1) return false;
            username = credentials[..separator];
            token = credentials[(separator + 1)..];
            return true;
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            return false;
        }
    }
}
