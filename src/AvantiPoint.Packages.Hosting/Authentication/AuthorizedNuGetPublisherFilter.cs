using System.Collections.Generic;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Hosting.Authentication;

public sealed class AuthorizedNuGetPublisherFilter : AuthorizedNuGetFilter
{
    private readonly PackageFeedOptions _options;

    public AuthorizedNuGetPublisherFilter(
        ILogger<AuthorizedNuGetPublisherFilter> logger,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor,
        IOptionsSnapshot<PackageFeedOptions> options)
        : base(logger, authentication, surfaceAccessor)
    {
        _options = options.Value;
    }

    protected override FeedOperation Operation => FeedOperation.Push;

    protected override FeedAuthenticationResult GetPreconditionFailure(HttpContext httpContext)
    {
        if (!_options.IsReadOnlyMode)
        {
            return null;
        }

        return FeedAuthenticationResult.Fail(
            "This server is in ReadOnly mode.",
            new Dictionary<string, string>
            {
                ["Server"] = httpContext.Request.Host.Value ?? string.Empty,
                ["X-Nuget-Warning"] = "This server is in ReadOnly mode."
            });
    }
}
