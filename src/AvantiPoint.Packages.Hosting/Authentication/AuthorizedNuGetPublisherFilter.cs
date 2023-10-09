using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Hosting.Authentication;

public sealed class AuthorizedNuGetPublisherFilter : AuthorizedNuGetFilter
{
    private PackageFeedOptions _options { get; }
    public AuthorizedNuGetPublisherFilter(ILogger<AuthorizedNuGetPublisherFilter> logger, IPackageAuthenticationService packageAuthentication, IOptionsSnapshot<PackageFeedOptions> options)
        : base(logger, packageAuthentication)
    {
        _options = options.Value;
    }

    protected override async Task<NuGetAuthenticationResult> IsAuthorized(IPackageAuthenticationService authenticationService)
    {
        if (_options.IsReadOnlyMode)
            return NuGetAuthenticationResult.Fail("This server is in ReadOnly mode.", HttpContext.Request.Host.Value);

        GetApiToken(out var apiKey);
        return await authenticationService.AuthenticateAsync(apiKey, default);
    }
}
