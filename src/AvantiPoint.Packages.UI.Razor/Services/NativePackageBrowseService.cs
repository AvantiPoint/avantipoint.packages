using System.Linq.Expressions;
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
        // Deduplicate and order exact names in SQL before paging. Provider
        // defaults may otherwise collapse Maven coordinates that differ by case.
        var collation = IdentityCollation;
        var identities = collation is null
            ? rows.Select(artifact => artifact.PackageName)
            : rows.Select(artifact => EF.Functions.Collate(artifact.PackageName, collation));
        var candidates = await identities.Distinct().OrderBy(name => name)
            .Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync(ct);
        var names = candidates.Take(PageSize).ToArray();
        // Browse never needs pubspec contents. Detail queries load only one package.
        var selected = FilterPackages(rows, names).Select(artifact => new NativeArtifact
        {
            FeedId = artifact.FeedId, Protocol = artifact.Protocol, PackageName = artifact.PackageName,
            Version = artifact.Version, Path = artifact.Path, ContentHash = artifact.ContentHash,
            Length = artifact.Length, ContentType = artifact.ContentType, PublishedUtc = artifact.PublishedUtc,
        });
        var visible = await ReadVisibleAsync(surface, selected, ct, exactPackages: names);
        var packages = MakePackages(surface, visible);
        return new(packages, page, candidates.Count > PageSize);
    }

    public async Task<NativePackageDetail?> GetPackageAsync(string protocol, string name, CancellationToken ct = default)
    {
        if (await AuthorizeAsync(protocol, ct) != 200) return null;
        var surface = GetSurface(protocol)!;
        var rows = await ReadVisibleAsync(surface,
            FilterPackages(QueryArtifacts(surface), [name]), ct, name);
        return MakePackages(surface, rows).SingleOrDefault();
    }

    private string? IdentityCollation => context.Database.ProviderName switch
    {
        "Microsoft.EntityFrameworkCore.SqlServer" => "Latin1_General_100_BIN2",
        "Microsoft.EntityFrameworkCore.Sqlite" => "BINARY",
        "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
        var provider when provider?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true => "utf8mb4_bin",
        _ => null,
    };

    private IQueryable<NativeArtifact> QueryArtifacts(SurfaceContext surface)
    {
        var kind = surface.Protocol.ToString();
        var collation = IdentityCollation;
        var rows = context.NativeArtifacts.AsNoTracking().Where(artifact => artifact.Version != null);
        return collation is null
            ? rows.Where(artifact => artifact.FeedId == surface.FeedId && artifact.Protocol == kind)
            : rows.Where(artifact => EF.Functions.Collate(artifact.FeedId, collation) == surface.FeedId
                && EF.Functions.Collate(artifact.Protocol, collation) == kind);
    }

    private IQueryable<NativeArtifact> FilterPackages(IQueryable<NativeArtifact> rows, IReadOnlyList<string> names)
    {
        // At most 50 comparisons. An OR predicate translates on providers that
        // cannot parameterize primitive collections used by Contains/IN.
        var artifact = Expression.Parameter(typeof(NativeArtifact), "artifact");
        Expression packageName = Expression.Property(artifact, nameof(NativeArtifact.PackageName));
        if (IdentityCollation is { } collation)
        {
            packageName = Expression.Call(typeof(RelationalDbFunctionsExtensions), nameof(RelationalDbFunctionsExtensions.Collate),
                [typeof(string)], Expression.Property(null, typeof(EF), nameof(EF.Functions)),
                packageName, Expression.Constant(collation));
        }
        Expression predicate = Expression.Constant(false);
        foreach (var name in names)
            predicate = Expression.OrElse(predicate, Expression.Equal(packageName, Expression.Constant(name)));
        return rows.Where(Expression.Lambda<Func<NativeArtifact, bool>>(predicate, artifact));
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
