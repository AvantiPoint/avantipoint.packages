using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci.Authentication;

public abstract class AuthorizedOciFilter : IEndpointFilter
{
    private readonly IFeedAuthenticationService _authentication;
    private readonly ISurfaceContextAccessor _surfaceAccessor;
    protected ILogger Logger { get; }

    protected AuthorizedOciFilter(
        ILogger logger,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor)
    {
        Logger = logger;
        _authentication = authentication;
        _surfaceAccessor = surfaceAccessor;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var surface = _surfaceAccessor.Current;
        if (surface is null || surface.Protocol != FeedProtocol.Oci)
        {
            return Results.NotFound();
        }

        try
        {
            var result = await _authentication.AuthenticateAsync(
                new FeedAuthenticationRequest(surface, context.HttpContext, Operation));

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
            Logger.LogError(ex, "OCI authentication failed.");
            return Results.BadRequest(new { errors = new[] { new { code = "UNAUTHORIZED", message = "OCI authentication failed." } } });
        }
    }

    protected abstract FeedOperation Operation { get; }

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
