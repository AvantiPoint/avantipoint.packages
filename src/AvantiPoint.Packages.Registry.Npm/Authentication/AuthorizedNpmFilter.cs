using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Npm.Authentication;

public abstract class AuthorizedNpmFilter : IEndpointFilter
{
    private readonly IFeedAuthenticationService _authentication;
    private readonly ISurfaceContextAccessor _surfaceAccessor;
    protected ILogger Logger { get; }

    protected AuthorizedNpmFilter(
        ILogger logger,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor)
    {
        Logger = logger;
        _authentication = authentication;
        _surfaceAccessor = surfaceAccessor;
    }

    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var surface = _surfaceAccessor.Current;
        if (surface is null || surface.Protocol != FeedProtocol.Npm)
        {
            return Results.NotFound();
        }

        try
        {
            var result = await IsAuthorized(surface, context.HttpContext);
            if (!result.Succeeded)
            {
                ApplyFailureHeaders(context.HttpContext, result);
                return Results.Unauthorized();
            }

            if (result.User is not null)
            {
                context.HttpContext.User = result.User;
            }

            return await next(context);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "npm authentication failed.");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    protected abstract FeedOperation Operation { get; }

    private async Task<FeedAuthenticationResult> IsAuthorized(SurfaceContext surface, HttpContext httpContext)
    {
        return await _authentication.AuthenticateAsync(
            new FeedAuthenticationRequest(surface, httpContext, Operation));
    }

    private static void ApplyFailureHeaders(HttpContext httpContext, FeedAuthenticationResult result)
    {
        if (result.ResponseHeaders is null)
        {
            return;
        }

        foreach (var (key, value) in result.ResponseHeaders)
        {
            httpContext.Response.Headers[key] = value;
        }
    }
}

public sealed class AuthorizedNpmPublisherFilter : AuthorizedNpmFilter
{
    public AuthorizedNpmPublisherFilter(
        ILogger<AuthorizedNpmPublisherFilter> logger,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor)
        : base(logger, authentication, surfaceAccessor)
    {
    }

    protected override FeedOperation Operation => FeedOperation.Push;
}

public sealed class AuthorizedNpmConsumerFilter : AuthorizedNpmFilter
{
    public AuthorizedNpmConsumerFilter(
        ILogger<AuthorizedNpmConsumerFilter> logger,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor)
        : base(logger, authentication, surfaceAccessor)
    {
    }

    protected override FeedOperation Operation => FeedOperation.Pull;
}
