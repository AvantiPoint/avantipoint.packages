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

        var auth = await TryAuthenticateTokenRequestAsync(httpContext, packageAuthentication, cancellationToken);
        if (!auth.Succeeded)
        {
            return Results.Unauthorized();
        }

        if (auth.User is not null)
        {
            httpContext.User = auth.User;
        }

        var token = ResolveIssuedToken(httpContext);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        var payload = new
        {
            token,
            access_token = token,
            expires_in = 3600,
            issued_at = DateTimeOffset.UtcNow,
        };

        return Results.Json(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static async Task<(bool Succeeded, System.Security.Claims.ClaimsPrincipal? User)> TryAuthenticateTokenRequestAsync(
        HttpContext httpContext,
        IPackageAuthenticationService packageAuthentication,
        CancellationToken cancellationToken)
    {
        if (TryGetBearerCredential(httpContext, out var bearerToken))
        {
            var bearerResult = await packageAuthentication.AuthenticateAsync(bearerToken, cancellationToken);
            if (bearerResult.Succeeded)
            {
                return (true, bearerResult.User);
            }
        }

        if (TryGetBasicPassword(httpContext, out var password))
        {
            var basicResult = await packageAuthentication.AuthenticateAsync(password, cancellationToken);
            if (basicResult.Succeeded)
            {
                return (true, basicResult.User);
            }
        }

        return (false, null);
    }

    private static string? ResolveIssuedToken(HttpContext httpContext)
    {
        if (TryGetBearerCredential(httpContext, out var bearerToken))
        {
            return bearerToken;
        }

        if (TryGetBasicPassword(httpContext, out var password))
        {
            return password;
        }

        return null;
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

    private static bool TryGetBasicPassword(HttpContext httpContext, out string password)
    {
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

            password = decoded[(separator + 1)..];
            return !string.IsNullOrEmpty(password);
        }
        catch
        {
            return false;
        }
    }

    private sealed class OciTokenEndpointFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next)
        {
            var surfaceAccessor = context.HttpContext.RequestServices.GetRequiredService<ISurfaceContextAccessor>();
            if (surfaceAccessor.Current is null || surfaceAccessor.Current.Protocol != FeedProtocol.Oci)
            {
                return Results.NotFound();
            }

            return await next(context);
        }
    }
}
