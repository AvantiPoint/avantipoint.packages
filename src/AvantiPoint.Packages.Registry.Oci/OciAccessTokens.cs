using System.Security.Cryptography;
using System.Text.Json;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci;

internal static class OciAccessTokens
{
    private const string Prefix = "apoci1.";

    internal static bool IsScoped(string token) => token.StartsWith(Prefix, StringComparison.Ordinal);

    internal static string Protect(HttpContext http, OciAccessTicket ticket, DateTimeOffset expires) =>
        Prefix + Protector(http).Protect(JsonSerializer.Serialize(ticket), expires);

    internal static bool TryRead(HttpContext http, string token, out OciAccessTicket ticket, out DateTimeOffset expires)
    {
        ticket = null!;
        expires = default;
        if (!IsScoped(token) || token.Length > 32_768) return false;
        try
        {
            var json = Protector(http).Unprotect(token[Prefix.Length..], out expires);
            var value = JsonSerializer.Deserialize<OciAccessTicket>(json);
            if (value is null || value.Grants is null || value.Credential is null) return false;
            ticket = value;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return false;
        }
    }

    internal static bool MatchesSurface(OciAccessTicket ticket, SurfaceContext surface) =>
        string.Equals(ticket.FeedId, surface.FeedId, StringComparison.Ordinal)
        && string.Equals(ticket.SurfaceId, surface.SurfaceId, StringComparison.Ordinal);

    internal static bool Allows(OciAccessTicket ticket, string repository, FeedOperation operation, string resourceType = "repository")
    {
        var action = resourceType == "registry" ? "*" : operation == FeedOperation.Push ? "push" : "pull";
        return ticket.Grants.Any(grant => string.Equals(grant.ResourceType, resourceType, StringComparison.Ordinal)
            && string.Equals(grant.Repository, repository, StringComparison.Ordinal)
            && grant.Actions.Contains(action, StringComparer.Ordinal));
    }

    internal static async Task<FeedAuthenticationResult> AuthenticateAsync(
        IFeedAuthenticationService authentication,
        FeedAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        var header = request.HttpContext.Request.Headers.Authorization;
        if (!System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(header.ToString(), out var parsed)
            || !string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || parsed.Parameter is not { } token || !IsScoped(token))
            return await authentication.AuthenticateAsync(request, cancellationToken);

        var permitted = TryRead(request.HttpContext, token, out var ticket, out _)
            && MatchesSurface(ticket, request.Surface)
            && (request.Operation == FeedOperation.Login
                || OciRouteParser.TryParse(request.HttpContext.Request.Path, out var route)
                    && (route.Kind == OciRouteKind.Catalog
                        ? request.Operation == FeedOperation.Pull && Allows(ticket, "catalog", request.Operation, "registry")
                        : route.RepositoryName is { } repository && Allows(ticket, repository, request.Operation)));
        try
        {
            request.HttpContext.Request.Headers.Authorization = permitted && ticket.Credential.Length > 0
                ? "Bearer " + ticket.Credential : Microsoft.Extensions.Primitives.StringValues.Empty;
            var result = await authentication.AuthenticateAsync(request, cancellationToken);
            // Recheck the original credential on every request so revocation, expiry,
            // account policy and feed grants still take effect immediately.
            return permitted ? result : FeedAuthenticationResult.Fail(
                "The scoped OCI token does not authorize this request.", result.ResponseHeaders);
        }
        finally
        {
            request.HttpContext.Request.Headers.Authorization = header;
        }
    }

    private static ITimeLimitedDataProtector Protector(HttpContext http) =>
        http.RequestServices.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("AvantiPoint.Packages.Registry.Oci.AccessToken.v1")
            .ToTimeLimitedDataProtector();
}
