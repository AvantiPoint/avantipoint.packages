using System.Net.Http.Headers;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Native.Authentication;

public sealed class NativeAuthorizationFilter(
    FeedProtocol protocol, FeedOperation operation) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;
        var surface = services.GetRequiredService<ISurfaceContextAccessor>().Current;
        if (surface?.Protocol != protocol) return Results.NotFound();
        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers.Vary = "Authorization";
        if (operation == FeedOperation.Push && services.GetRequiredService<IOptions<PackageFeedOptions>>().Value.IsReadOnlyMode)
            return Results.StatusCode(403);
        var options = services.GetRequiredService<IOptions<FeedOptions>>().Value;
        if (operation == FeedOperation.Pull && options.Authentication.AllowAnonymousPull)
            return await next(context);

        var challenge = protocol == FeedProtocol.Pub
            ? "Bearer realm=\"pub\"" : "Basic realm=\"AvantiPoint Packages\", charset=\"UTF-8\"";
        if (!TryCredentials(http, protocol, out var username, out var token))
        {
            http.Response.Headers.WWWAuthenticate = challenge;
            return Results.Unauthorized();
        }

        // Hosts opt into operation-aware validation. Never silently treat a
        // publisher API key as a read-only consumer token or bypass custom auth.
        var authentication = services.GetService<IFeedTokenAuthenticationService>();
        if (authentication is null)
            return Results.Problem(statusCode: 503, title: "Native feed authentication is not configured.");
        var result = await authentication.AuthenticateTokenAsync(token, operation, username, http.RequestAborted);
        if (!result.Succeeded)
        {
            if (result.ResponseHeaders is not null)
                foreach (var (name, value) in result.ResponseHeaders) http.Response.Headers[name] = value;
            if (!http.Response.Headers.ContainsKey("WWW-Authenticate")) http.Response.Headers.WWWAuthenticate = challenge;
            return Results.StatusCode(result.FailureStatusCode == 403 ? 403 : 401);
        }
        if (result.User is not null) http.User = result.User;
        if (operation == FeedOperation.Push)
        {
            var feature = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false })
            {
                var limits = services.GetRequiredService<IOptionsMonitor<NativeRegistryOptions>>().Get(protocol.ToString());
                feature.MaxRequestBodySize = limits.MaxArtifactBytes + (protocol == FeedProtocol.Pub ? 64 * 1024 : 0);
            }
        }
        return await next(context);
    }

    private static bool TryCredentials(HttpContext http, FeedProtocol protocol, out string? username, out string token)
    {
        username = null;
        token = string.Empty;
        if (!AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization, out var header)) return false;
        if (header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            token = header.Parameter ?? string.Empty;
            return token.Length > 0;
        }
        if (protocol == FeedProtocol.Pub || !header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var credentials = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(header.Parameter ?? ""));
            var separator = credentials.IndexOf(':');
            if (separator <= 0 || separator == credentials.Length - 1) return false;
            username = credentials[..separator];
            token = credentials[(separator + 1)..];
            return true;
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException) { return false; }
    }
}
