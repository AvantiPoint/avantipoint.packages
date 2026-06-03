using System.Net.Http.Headers;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Feed.Platform.Authentication;

public sealed class NpmFeedAuthenticationAdapter : IFeedProtocolAuthenticationAdapter
{
    private readonly IPackageAuthenticationService _packageAuthentication;
    private readonly IOptions<FeedOptions> _feedOptions;

    public NpmFeedAuthenticationAdapter(
        IPackageAuthenticationService packageAuthentication,
        IOptions<FeedOptions> feedOptions)
    {
        _packageAuthentication = packageAuthentication;
        _feedOptions = feedOptions;
    }

    public FeedProtocol Protocol => FeedProtocol.Npm;

    public async Task<FeedAuthenticationResult> AuthenticateAsync(
        FeedAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Operation == FeedOperation.Pull
            && _feedOptions.Value.Authentication.AllowAnonymousPull)
        {
            return FeedAuthenticationResult.Success();
        }

        if (!TryGetBearerToken(request.HttpContext, out var token))
        {
            return FeedAuthenticationResult.Fail(
                "Bearer token required.",
                new Dictionary<string, string>
                {
                    ["WWW-Authenticate"] = "Bearer",
                });
        }

        // API key is validated via IPackageAuthenticationService (shared with NuGet).
        var result = await _packageAuthentication.AuthenticateAsync(token, cancellationToken);
        if (result.Succeeded)
        {
            return FeedAuthenticationResult.Success(result.User);
        }

        return FeedAuthenticationResult.Fail(result.Message ?? "Invalid token.");
    }

    private static bool TryGetBearerToken(HttpContext http, out string token)
    {
        token = string.Empty;
        try
        {
            var authorization = http.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authorization))
            {
                return false;
            }

            var header = AuthenticationHeaderValue.Parse(authorization);
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
}
