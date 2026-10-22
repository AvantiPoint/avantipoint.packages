using AvantiPoint.Packages.Host.Admin.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AvantiPoint.Packages.Host;

internal static class HostMonitoringPrivacy
{
    internal static async ValueTask<object?> FilterHealthAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (await CanBrowseAsync(context.HttpContext)) return await next(context);
        // Keep readiness codes without disclosing feed existence, surfaces, or check details.
        var report = await context.HttpContext.RequestServices.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(context.HttpContext.RequestAborted);
        return Results.Json(new { status = report.Status.ToString().ToLowerInvariant() },
            statusCode: report.Status == HealthStatus.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<bool> CanBrowseAsync(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true &&
        (await context.RequestServices.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(context.User, null, HostUiAuthorizationPolicy.Name)).Succeeded;
}
