using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Feed.Platform.Authentication;

public sealed class OciFeedAuthenticationAdapter : IFeedProtocolAuthenticationAdapter
{
    private readonly IPackageAuthenticationService _packageAuthentication;
    private readonly IOptions<FeedOptions> _feedOptions;

    public OciFeedAuthenticationAdapter(
        IPackageAuthenticationService packageAuthentication,
        IOptions<FeedOptions> feedOptions)
    {
        _packageAuthentication = packageAuthentication;
        _feedOptions = feedOptions;
    }

    public FeedProtocol Protocol => FeedProtocol.Oci;

    public async Task<FeedAuthenticationResult> AuthenticateAsync(
        FeedAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Operation == FeedOperation.Pull
            && _feedOptions.Value.Authentication.AllowAnonymousPull)
        {
            return FeedAuthenticationResult.Success();
        }

        if (TryGetBearerToken(request.HttpContext, out var bearerToken))
        {
            var bearerResult = await _packageAuthentication.AuthenticateAsync(bearerToken, cancellationToken);
            if (bearerResult.Succeeded)
            {
                return FeedAuthenticationResult.Success(bearerResult.User);
            }
        }

        if (TryGetBasicCredentials(request.HttpContext, out var username, out var password))
        {
            var basicResult = await _packageAuthentication.AuthenticateAsync(password, cancellationToken);
            if (basicResult.Succeeded)
            {
                return FeedAuthenticationResult.Success(basicResult.User);
            }
        }

        var realm = request.Surface.PublicBaseUrl.ToString().TrimEnd('/');
        return FeedAuthenticationResult.Fail(
            "Authentication required.",
            new Dictionary<string, string>
            {
                ["WWW-Authenticate"] = $"Bearer realm=\"{realm}\",service=\"{request.Surface.FeedId}\"",
            });
    }

    private static bool TryGetBearerToken(HttpContext http, out string token)
    {
        token = null!;
        try
        {
            var header = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(http.Request.Headers.Authorization);
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

    private static bool TryGetBasicCredentials(HttpContext http, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        try
        {
            var header = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(http.Request.Headers.Authorization);
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
