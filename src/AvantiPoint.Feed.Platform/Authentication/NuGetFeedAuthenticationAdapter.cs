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
        NuGetAuthenticationResult result;

        if (TryGetApiKey(http, out var apiKey))
        {
            result = await _packageAuthentication.AuthenticateAsync(apiKey, cancellationToken);
        }
        else if (TryGetBasicCredentials(http, out var username, out var password))
        {
            result = await _packageAuthentication.AuthenticateAsync(username, password, cancellationToken);
        }
        else
        {
            result = NuGetAuthenticationResult.Fail("No credentials provided.", http.Request.Host.Value);
        }

        if (result.Succeeded)
        {
            return FeedAuthenticationResult.Success(result.User);
        }

        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(result.Realm))
        {
            headers["WWW-Authenticate"] = FormatBasicRealm(result.Realm);
        }

        headers["X-Nuget-Warning"] = result.Message;
        headers["Server"] = result.Server;

        return FeedAuthenticationResult.Fail(result.Message, headers);
    }

    private static bool TryGetApiKey(HttpContext http, out string apiKey)
    {
        apiKey = http.Request.Headers[ApiKeyHeader];
        return !string.IsNullOrEmpty(apiKey);
    }

    private static bool TryGetBasicCredentials(HttpContext http, out string username, out string password)
    {
        username = null;
        password = null;
        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(http.Request.Headers.Authorization);
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
