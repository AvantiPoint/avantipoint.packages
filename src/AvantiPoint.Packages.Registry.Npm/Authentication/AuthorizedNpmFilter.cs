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

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var surface = _surfaceAccessor.Current;
        if (surface is null || surface.Protocol != FeedProtocol.Npm)
        {
            return Results.NotFound();
        }

        FeedAuthenticationResult result;
        try
        {
            result = await IsAuthorized(surface, context.HttpContext);
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "npm authentication could not be evaluated.");
            return Results.Problem(
                title: "npm authentication could not be evaluated.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!result.Succeeded)
        {
            ApplyFailureHeaders(context.HttpContext, result);
            return Results.Unauthorized();
        }

        if (result.User is not null)
        {
            context.HttpContext.User = result.User;
        }

        // Exceptions and client cancellations raised by the registry handler are not
        // authentication failures. Let the normal ASP.NET Core pipeline handle them.
        return await next(context);
    }

    protected abstract FeedOperation Operation { get; }

    private async Task<FeedAuthenticationResult> IsAuthorized(SurfaceContext surface, HttpContext httpContext)
    {
        return await _authentication.AuthenticateAsync(
            new FeedAuthenticationRequest(surface, httpContext, Operation),
            httpContext.RequestAborted);
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
