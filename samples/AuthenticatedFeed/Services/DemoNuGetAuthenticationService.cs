using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;

namespace AuthenticatedFeed.Services
{
    public class DemoNuGetAuthenticationService : IPackageAuthenticationService
    {
        private ClaimsPrincipal DemoUser { get; }

        public DemoNuGetAuthenticationService()
        {
            var identity = new ClaimsIdentity("NuGetAuth");
            identity.AddClaim(new Claim(ClaimTypes.Name, "Demo User"));
            identity.AddClaim(new Claim(ClaimTypes.Email, "demo@user.com"));
            DemoUser = new ClaimsPrincipal(identity);
        }

        public Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
        {
            var result = apiKey == "12345" ? NuGetAuthenticationResult.Success(DemoUser) : NuGetAuthenticationResult.Fail("Unauthorized apiKey", "Demo Authenticated Feed");
            return Task.FromResult(result);
        }

        public Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken)
        {
            var result = username == "skroob" && token == "12345" ? NuGetAuthenticationResult.Success(DemoUser) : NuGetAuthenticationResult.Fail("Invalid username or token", "Demo Authenticated Feed");
            return Task.FromResult(result);
        }
    }
}
