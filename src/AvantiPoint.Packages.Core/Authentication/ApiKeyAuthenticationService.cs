using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core
{
    public class ApiKeyAuthenticationService : IPackageAuthenticationService
    {
        private readonly string _apiKey;

        public ApiKeyAuthenticationService(IOptionsSnapshot<APPackagesOptions> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _apiKey = string.IsNullOrEmpty(options.Value.ApiKey) ? null : options.Value.ApiKey;
        }

        public Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken) => 
            Task.FromResult(Authenticate(apiKey));

        public Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken) =>
            Task.FromResult(NuGetAuthenticationResult.Success());

        private NuGetAuthenticationResult Authenticate(string apiKey)
        {
            // No authentication is necessary if there is no required API key.
            if (string.IsNullOrEmpty(_apiKey)) 
                return NuGetAuthenticationResult.Success();

            if (string.IsNullOrEmpty(apiKey))
                return NuGetAuthenticationResult.Fail("No Api Token provided.", "AvantiPoint Package Server");

            return _apiKey == apiKey ? NuGetAuthenticationResult.Success() : NuGetAuthenticationResult.Fail("Invalid Api Token provided.", "AvantiPoint Package Server");
        }
    }
}
