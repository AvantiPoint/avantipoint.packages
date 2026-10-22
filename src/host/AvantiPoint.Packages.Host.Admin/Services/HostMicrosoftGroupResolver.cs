using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services;

/// <summary>
/// Resolves only the groups used by this host's access policy. Graph's delegated
/// /me/checkMemberGroups endpoint includes nested memberships and requires User.Read.
/// It also works when the OAuth principal has no groups or has group-overage claims.
/// </summary>
public sealed class HostMicrosoftGroupResolver(
    IHttpClientFactory httpClientFactory,
    ILogger<HostMicrosoftGroupResolver> logger)
{
    public const string HttpClientName = nameof(HostMicrosoftGroupResolver);
    private const string Endpoint = "https://graph.microsoft.com/v1.0/me/checkMemberGroups";

    public static string[] ConfiguredGroups(HostMicrosoftOptions options) =>
        options.RequiredGroupIds
            .Concat(options.AdminRoleGroupIds)
            .Concat(options.PublisherRoleGroupIds)
            .Concat(options.ConsumerRoleGroupIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public async Task<HostLoginValidationResult> ResolveAsync(
        ClaimsPrincipal principal,
        HostMicrosoftOptions options,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var configured = ConfiguredGroups(options);
        if (configured.Length == 0)
        {
            return HostLoginValidationResult.Success();
        }

        if (configured.Any(group => !Guid.TryParse(group, out _)))
        {
            return HostLoginValidationResult.Fail("Entra group configuration must use group object IDs.");
        }

        if (string.IsNullOrWhiteSpace(accessToken) || principal.Identity is not ClaimsIdentity identity)
        {
            return HostLoginValidationResult.Fail("Entra group membership could not be verified. Sign in again.");
        }

        var memberships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            // Graph accepts at most twenty group IDs per request. Do not follow any
            // endpoint embedded in overage claims or send this token to another host.
            foreach (var batch in configured.Chunk(20))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = JsonContent.Create(new { groupIds = batch }),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Entra group verification returned HTTP {StatusCode}", (int)response.StatusCode);
                    return VerificationFailed();
                }

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if (!document.RootElement.TryGetProperty("value", out var values)
                    || values.ValueKind != JsonValueKind.Array)
                {
                    return VerificationFailed();
                }

                foreach (var value in values.EnumerateArray())
                {
                    if (value.ValueKind != JsonValueKind.String
                        || value.GetString() is not { } group
                        || !batch.Contains(group, StringComparer.OrdinalIgnoreCase))
                    {
                        return VerificationFailed();
                    }
                    memberships.Add(group);
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                  || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Never log access tokens or Graph response bodies.
            logger.LogWarning("Entra group verification failed ({FailureType})", ex.GetType().Name);
            return VerificationFailed();
        }

        // Replace claims only after every request succeeds, before provisioning can
        // change persisted roles. A directory outage must not demote existing users.
        foreach (var claimsIdentity in principal.Identities)
        {
            foreach (var claim in claimsIdentity.FindAll("groups").ToArray())
            {
                claimsIdentity.RemoveClaim(claim);
            }
        }
        identity.AddClaims(memberships.Select(group => new Claim("groups", group)));
        return HostLoginValidationResult.Success();
    }

    private static HostLoginValidationResult VerificationFailed() =>
        HostLoginValidationResult.Fail(
            "Entra group membership could not be verified. Try again, or ask an administrator to check the app's Microsoft Graph User.Read access and group configuration.");
}
