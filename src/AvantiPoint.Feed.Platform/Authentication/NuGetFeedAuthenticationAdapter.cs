using System.Net.Http.Headers;
using System.Text;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Feed.Platform.Authentication;

public sealed class NuGetFeedAuthenticationAdapter : IFeedProtocolAuthenticationAdapter
{
    public const string ApiKeyHeader = "X-NuGet-ApiKey";
    private const string ConsumerRole = "PackageConsumer";
    private const string AdministratorRole = "Admin";

    private readonly IPackageAuthenticationService _packageAuthentication;
    private readonly IOptions<FeedOptions> _feedOptions;

    public NuGetFeedAuthenticationAdapter(
        IPackageAuthenticationService packageAuthentication,
        IOptions<FeedOptions> feedOptions)
    {
        _packageAuthentication = packageAuthentication;
        _feedOptions = feedOptions;
    }

    public FeedProtocol Protocol => FeedProtocol.NuGet;

    public async Task<FeedAuthenticationResult> AuthenticateAsync(
        FeedAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Operation == FeedOperation.Pull
            && _feedOptions.Value.Authentication.AllowAnonymousPull)
        {
            return FeedAuthenticationResult.Success();
        }

        var http = request.HttpContext;
        if (request.Operation == FeedOperation.Pull
            && http.User.Identity?.IsAuthenticated == true
            && (http.User.IsInRole(ConsumerRole) || http.User.IsInRole(AdministratorRole)))
        {
            // A person browsing the authenticated Host already has an application cookie and
            // a role provisioned from the same package-account record. Honor that principal for
            // browser GETs so download links work without weakening native-client token auth.
            return FeedAuthenticationResult.Success(http.User);
        }

        NuGetAuthenticationResult result;

        if (TryGetApiKey(http, out var apiKey))
        {
            result = await _packageAuthentication.AuthenticateForOperationAsync(
                apiKey,
                request.Operation,
                cancellationToken);
        }
        else if (TryGetBasicCredentials(http, out var username, out var password))
        {
            result = await _packageAuthentication.AuthenticateForOperationAsync(
                username,
                password,
                request.Operation,
                cancellationToken);
        }
        else
        {
            result = NuGetAuthenticationResult.Fail("No credentials provided.", http.Request.Host.Value ?? string.Empty);
        }

        if (result.Succeeded)
        {
            return FeedAuthenticationResult.Success(result.User);
        }

        var headers = new Dictionary<string, string>();
        var realm = string.IsNullOrWhiteSpace(result.Realm)
            ? http.Request.Host.Value
            : result.Realm;
        if (!string.IsNullOrEmpty(realm))
        {
            headers["WWW-Authenticate"] = FormatBasicRealm(realm);
        }

        headers["X-Nuget-Warning"] = result.Message ?? string.Empty;
        headers["Server"] = result.Server ?? string.Empty;

        return FeedAuthenticationResult.Fail(result.Message ?? "Authentication failed.", headers);
    }

    private static bool TryGetApiKey(HttpContext http, out string apiKey)
    {
        apiKey = http.Request.Headers[ApiKeyHeader].ToString();
        return !string.IsNullOrEmpty(apiKey);
    }

    private static bool TryGetBasicCredentials(HttpContext http, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;
        try
        {
            var authorization = http.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authorization))
            {
                return false;
            }

            var authHeader = AuthenticationHeaderValue.Parse(authorization);
            if (!string.Equals(authHeader.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var credentialBytes = Convert.FromBase64String(authHeader.Parameter ?? string.Empty);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            username = credentials[0];
            password = credentials.Length > 1 ? credentials[1] : string.Empty;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatBasicRealm(string realm)
    {
        if (realm.StartsWith("Basic realm =\"", StringComparison.Ordinal))
        {
            return realm;
        }

        if (realm.Contains('"'))
        {
            var i = realm.IndexOf('"');
            realm = realm[i..].Replace("\"", string.Empty, StringComparison.Ordinal);
        }

        return $"Basic realm =\"{realm}\"";
    }
}
