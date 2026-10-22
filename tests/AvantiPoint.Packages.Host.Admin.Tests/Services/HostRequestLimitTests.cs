using System.Net;
using AvantiPoint.Packages.Host.Admin.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AvantiPoint.Packages.Host.Admin.Tests.Services;

public sealed class HostRequestLimitTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RateLimits_SignInSeparatelyFromPackageRequests(bool enabled)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Host:RequestLimits:Enabled"] = enabled.ToString(),
            ["Host:RequestLimits:SignInsPerMinute"] = "1",
            ["Host:RequestLimits:RequestsPerMinute"] = "100",
        });
        builder.Services.AddHostRequestLimits(builder.Configuration);
        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/Account/Login", () => "login");
        app.MapGet("/package", () => "package");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var first = await client.GetAsync("Account/Login", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var second = await client.GetAsync("Account/Login", TestContext.Current.CancellationToken);
        Assert.Equal(enabled ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK, second.StatusCode);
        if (enabled) Assert.NotNull(second.Headers.RetryAfter);
        using var package = await client.GetAsync("package", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, package.StatusCode);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}
