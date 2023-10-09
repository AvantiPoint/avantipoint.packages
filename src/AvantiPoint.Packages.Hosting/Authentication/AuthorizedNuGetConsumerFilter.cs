using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Authentication;

public sealed class AuthorizedNuGetConsumerFilter : AuthorizedNuGetFilter
{
    public AuthorizedNuGetConsumerFilter(ILogger<AuthorizedNuGetConsumerFilter> logger, IPackageAuthenticationService packageAuthentication) 
        : base(logger, packageAuthentication)
    {
    }

    protected override async Task<NuGetAuthenticationResult> IsAuthorized(IPackageAuthenticationService authenticationService)
    {
        GetUserCredentials(out var username, out var password);
        var result = await authenticationService.AuthenticateAsync(username, password, default);

        if (!result.Succeeded && string.IsNullOrEmpty(result.Realm))
        {
            return NuGetAuthenticationResult.Fail(result.Message, result.Server, "AvantiPoint Package Feed");
        }

        return result;
    }
}
