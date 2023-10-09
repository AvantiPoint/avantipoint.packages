using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core
{
    public class ApiKeyAuthenticationService : IPackageAuthenticationService
    {
        private readonly string _apiKey;
        private readonly string _serverName;

        public ApiKeyAuthenticationService(IOptionsSnapshot<PackageFeedOptions> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _apiKey = string.IsNullOrEmpty(options.Value.ApiKey) ? null : options.Value.ApiKey;
            _serverName = string.IsNullOrEmpty(options.Value.Shield?.ServerName) ? "AvantiPoint Package Server" : options.Value.Shield?.ServerName;
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
                return NuGetAuthenticationResult.Fail("No Api Token provided.", _serverName);

            return _apiKey == apiKey ? NuGetAuthenticationResult.Success() : NuGetAuthenticationResult.Fail("Invalid Api Token provided.", _serverName);
        }
    }
}
