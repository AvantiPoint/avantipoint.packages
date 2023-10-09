using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Authentication;

public abstract class AuthorizedNuGetFilter : IEndpointFilter
{
    public const string ApiKeyHeader = "X-NuGet-ApiKey";

    private IPackageAuthenticationService _packageAuthentication { get; }
    protected ILogger Logger { get; }

    protected HttpContext HttpContext { get; private set; }

    protected AuthorizedNuGetFilter(ILogger logger, IPackageAuthenticationService packageAuthentication)
    {
        Logger = logger;
        _packageAuthentication = packageAuthentication;
    }

    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext = context.HttpContext;
        try
        {
            if (_packageAuthentication is not null)
            {
                var result = await IsAuthorized(_packageAuthentication);
                if (!result.Succeeded)
                {
                    SetFailedResponse(result);
                    return Results.Unauthorized();
                }

                if (result.User is not null)
                    HttpContext.User = result.User;
            }

            return await next(context);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred while processing the user authentication.");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    protected abstract Task<NuGetAuthenticationResult> IsAuthorized(IPackageAuthenticationService authenticationService);

    protected void GetApiToken(out string apiKey)
    {
        try
        {
            apiKey = HttpContext.Request.Headers[ApiKeyHeader];
        }
        catch
        {
            apiKey = null;
        }
    }

    protected void GetUserCredentials(out string username, out string password)
    {
        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(HttpContext.Request.Headers["Authorization"]);
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
            username = credentials[0];
            password = credentials[1];
        }
        catch
        {
            username = null;
            password = null;
        }
    }

    private void SetFailedResponse(NuGetAuthenticationResult result)
    {
        if (!string.IsNullOrEmpty(result.Realm))
            HttpContext.Response.Headers.Append("Www-Authenticate", GetRealm(result.Realm));

        HttpContext.Response.Headers.Append("X-Frame-Options", "Deny");
        HttpContext.Response.Headers.Append("X-Nuget-Warning", result.Message);
        HttpContext.Response.Headers.Append("Server", result.Server);
        HttpContext.Response.StatusCode = 401;

    }

    private string GetRealm(string realm)
    {
        if (realm.StartsWith("Basic realm =\""))
            return realm;
        else if (realm.Contains('"'))
        {
            var i = realm.IndexOf('"');
            realm = Regex.Replace(realm.Substring(i), "\"", string.Empty);
        }
        return $"Basic realm =\"{realm}\"";
    }
}
