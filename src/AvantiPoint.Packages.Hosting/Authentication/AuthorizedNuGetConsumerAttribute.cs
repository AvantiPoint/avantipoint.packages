using System.Threading.Tasks;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Hosting.Authentication
{
    public class AuthorizedNuGetConsumerAttribute : AuthorizedNuGetActionFilterAttribute
    {
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
}
