using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public sealed class HostRequestLimitOptions
{
    public bool Enabled { get; set; } = true;
    public int RequestsPerMinute { get; set; } = 6000;
    public int SignInsPerMinute { get; set; } = 30;
    public int ConcurrentRequests { get; set; } = 128;
}

public static class HostRequestLimitExtensions
{
    public static IServiceCollection AddHostRequestLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection("Host:RequestLimits").Get<HostRequestLimitOptions>() ?? new();
        if (limits.RequestsPerMinute <= 0 || limits.SignInsPerMinute <= 0 || limits.ConcurrentRequests <= 0)
            throw new InvalidOperationException("Host:RequestLimits values must be positive.");
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var delay = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 1;
                context.HttpContext.Response.Headers.RetryAfter = delay.ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            if (!limits.Enabled) return;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var signIn = IsSignIn(context.Request.Path);
                    var key = $"{context.Connection.RemoteIpAddress}:{signIn}";
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = signIn ? limits.SignInsPerMinute : limits.RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1), AutoReplenishment = true, QueueLimit = 0,
                    });
                }),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    // A Blazor circuit holds a websocket for its lifetime; it is not an API request.
                    context.Request.Path.StartsWithSegments("/_blazor")
                        ? RateLimitPartition.GetNoLimiter("interactive")
                        : RateLimitPartition.GetConcurrencyLimiter("requests", _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = limits.ConcurrentRequests, QueueLimit = 0,
                        })));
        });
        return services;
    }

    private static bool IsSignIn(PathString path) =>
        path.StartsWithSegments("/Account/Login") || path.StartsWithSegments("/Account/ExternalLogin")
        || path.Value?.StartsWith("/signin-", StringComparison.OrdinalIgnoreCase) == true;
}
