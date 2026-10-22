using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci;

public static class OciTokenEndpoints
{
    public static void MapOciTokenRoutes(this WebApplication app, IFeedRegistry registry)
    {
        app.MapGet("/token", IssueTokenAsync).AddEndpointFilter<OciTokenEndpointFilter>();

        foreach (var surface in registry.Surfaces.Where(s => s.Protocol == FeedProtocol.Oci && !string.IsNullOrEmpty(s.OciSegment)))
        {
            var prefix = surface.RoutePrefix.TrimEnd('/');
            if (!prefix.StartsWith('/'))
            {
                prefix = "/" + prefix;
            }

            app.MapGet($"{prefix}/token", IssueTokenAsync).AddEndpointFilter<OciTokenEndpointFilter>();
        }
    }

    private static async Task<IResult> IssueTokenAsync(
        HttpContext httpContext,
        ISurfaceContextAccessor surfaceAccessor,
        IPackageAuthenticationService packageAuthentication,
        CancellationToken cancellationToken)
    {
        var surface = surfaceAccessor.Current;
        if (surface is null || surface.Protocol != FeedProtocol.Oci)
        {
            return Results.NotFound();
        }

        var service = httpContext.Request.Query["service"];
        if (service.Count > 1 || service.Count == 1 && !string.Equals(service[0], surface.FeedId, StringComparison.Ordinal))
            return Results.Unauthorized();
        if (!TryGetRequestedScopes(httpContext, out var requested))
            return Results.BadRequest(new { error = "invalid_scope" });

        string? username = null;
        string credential;
        if (!TryGetBearerCredential(httpContext, out credential)
            && !TryGetBasicCredentials(httpContext, out username, out credential))
        {
            if (httpContext.Request.Headers.ContainsKey("Authorization")) return Results.Unauthorized();
            username = null;
            credential = string.Empty;
        }

        OciAccessTicket? parent = null;
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        if (OciAccessTokens.IsScoped(credential))
        {
            if (!OciAccessTokens.TryRead(httpContext, credential, out parent, out var parentExpires)
                || !OciAccessTokens.MatchesSurface(parent, surface)) return Results.Unauthorized();
            credential = parent.Credential;
            if (parentExpires < expires) expires = parentExpires;
        }

        if (!TryNormalizeRepositoryScopes(surface, requested))
            return Results.BadRequest(new { error = "invalid_scope" });

        Task<NuGetAuthenticationResult> Authenticate(FeedOperation operation) => username is null
            ? packageAuthentication.AuthenticateForOperationAsync(credential, operation, cancellationToken)
            : packageAuthentication.AuthenticateForOperationAsync(username, credential, operation, cancellationToken);

        // Authenticate identity separately from resource authorization. A valid
        // consumer requesting pull,push receives pull; an invalid key still fails.
        var identity = await Authenticate(FeedOperation.Login);
        if (!identity.Succeeded) return Results.Unauthorized();
        if (identity.User is not null) httpContext.User = identity.User;

        var pull = requested.Any(scope => scope.Actions.Contains("pull") || scope.ResourceType == "registry")
            && (await Authenticate(FeedOperation.Pull)).Succeeded;
        var push = requested.Any(scope => scope.Actions.Contains("push"))
            && (await Authenticate(FeedOperation.Push)).Succeeded;
        var grants = requested.Select(scope => new OciAccessGrant(scope.Repository,
            scope.Actions.Where(action =>
            {
                var operation = action == "push" ? FeedOperation.Push : FeedOperation.Pull;
                return (operation == FeedOperation.Push ? push : pull)
                    && (parent is null || OciAccessTokens.Allows(parent, scope.Repository, operation, scope.ResourceType));
            }).ToArray(), scope.ResourceType)).Where(scope => scope.Actions.Length > 0).ToArray();
        var ticket = new OciAccessTicket(surface.FeedId, surface.SurfaceId, credential, grants);
        var token = OciAccessTokens.Protect(httpContext, ticket, expires);
        return Results.Json(new
        {
            token,
            access_token = token,
            expires_in = Math.Max(0, (int)(expires - DateTimeOffset.UtcNow).TotalSeconds),
            issued_at = DateTimeOffset.UtcNow,
            scope = string.Join(" ", grants.Select(scope =>
                $"{scope.ResourceType}:{PublicRepositoryName(surface, scope)}:{string.Join(",", scope.Actions)}")),
        });
    }

    // Tickets retain exact internal repository identities, including existing tickets.
    // Translate only newly requested wire names, once, before permission intersection
    // and parent-token checks; never reinterpret a parent's stored grants.
    private static bool TryNormalizeRepositoryScopes(SurfaceContext surface, OciAccessGrant[] scopes)
    {
        if (string.IsNullOrEmpty(surface.OciSegment)) return true;
        var prefix = surface.OciSegment + "/";
        for (var index = 0; index < scopes.Length; index++)
        {
            var scope = scopes[index];
            if (scope.ResourceType != "repository") continue;
            if (!scope.Repository.StartsWith(prefix, StringComparison.Ordinal)) return false;
            scopes[index] = scope with { Repository = scope.Repository[prefix.Length..] };
        }
        return true;
    }

    private static string PublicRepositoryName(SurfaceContext surface, OciAccessGrant scope) =>
        scope.ResourceType == "repository" && !string.IsNullOrEmpty(surface.OciSegment)
            ? surface.OciSegment + "/" + scope.Repository : scope.Repository;

    private static bool TryGetRequestedScopes(HttpContext http, out OciAccessGrant[] scopes)
    {
        scopes = [];
        var requested = http.Request.Query["scope"];
        if (requested.Sum(value => value?.Length ?? 0) > 8_192) return false;
        var grants = new Dictionary<(string Type, string Name), HashSet<string>>();
        foreach (var value in requested)
        foreach (var entry in (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = entry.Split(':');
            if (fields.Length != 3 || fields[1].Length is 0 or > 255) return false;
            var actions = fields[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (actions.Length == 0) return false;
            if (fields[0] == "registry")
            {
                if (fields[1] != "catalog" || actions.Any(action => action != "*")) return false;
            }
            else if (fields[0] != "repository" || fields[1].Split('/').Any(component =>
                component.Length == 0 || component is "." or ".."
                || component.Any(c => !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c is not ('.' or '_' or '-')))
                || actions.Any(action => action is not ("pull" or "push"))) return false;
            var key = (fields[0], fields[1]);
            if (!grants.TryGetValue(key, out var merged))
            {
                if (grants.Count == 16) return false;
                grants[key] = merged = new HashSet<string>(StringComparer.Ordinal);
            }
            merged.UnionWith(actions);
        }
        scopes = grants.Select(entry => new OciAccessGrant(entry.Key.Name,
            entry.Value.Order(StringComparer.Ordinal).ToArray(), entry.Key.Type)).ToArray();
        return true;
    }

    private static bool TryGetBearerCredential(HttpContext httpContext, out string token)
    {
        token = string.Empty;
        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var values))
        {
            return false;
        }

        try
        {
            var header = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(values.ToString());
            if (!string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            token = header.Parameter ?? string.Empty;
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetBasicCredentials(
        HttpContext httpContext,
        out string username,
        out string password)
    {
        username = string.Empty;
        password = string.Empty;
        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var values))
        {
            return false;
        }

        try
        {
            var header = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(values.ToString());
            if (!string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(header.Parameter))
            {
                return false;
            }

            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            var separator = decoded.IndexOf(':');
            if (separator < 0)
            {
                return false;
            }

            username = decoded[..separator];
            password = decoded[(separator + 1)..];
            return !string.IsNullOrEmpty(password);
        }
        catch
        {
            return false;
        }
    }

}
