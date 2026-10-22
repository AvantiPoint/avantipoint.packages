using System.Net;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Configuration;

public sealed class HostReverseProxyOptionsTests
{
    [Theory]
    [InlineData("::ffff:100.100.0.8", "https", "packages.example.com")]
    [InlineData("::ffff:100.100.0.138", "https", "packages.example.com")]
    [InlineData("203.0.113.8", "http", "internal")]
    public async Task AzureOverlay_TrustsOnlyTheConfiguredHop(string proxy, string expectedScheme, string expectedHost)
    {
        var options = new ForwardedHeadersOptions();
        new HostReverseProxyOptions
        {
            KnownNetworks = ["10.42.0.0/23", "100.100.0.0/16"],
            AllowedHosts = ["packages.example.com"],
        }.Apply(options);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(proxy);
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("internal");
        // A client-supplied earlier hop must not override the immediate trusted hop.
        context.Request.Headers["X-Forwarded-For"] = "192.0.2.2, 198.51.100.9";
        context.Request.Headers["X-Forwarded-Proto"] = "http, https";
        context.Request.Headers["X-Forwarded-Host"] = "attacker.example, packages.example.com";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            NullLoggerFactory.Instance, Options.Create(options));

        await middleware.Invoke(context);

        Assert.Equal(expectedScheme, context.Request.Scheme);
        Assert.Equal(expectedHost, context.Request.Host.Value);
        Assert.Equal(expectedScheme == "https" ? "198.51.100.9" : proxy,
            context.Connection.RemoteIpAddress!.ToString());
    }

    [Fact]
    public void Apply_AddsOnlyExplicitlyConfiguredProxyAddressesAndNetworks()
    {
        var options = new ForwardedHeadersOptions();
        var configuration = new HostReverseProxyOptions
        {
            ForwardLimit = 2,
            KnownProxies = ["10.2.0.8"],
            KnownNetworks = ["10.42.0.0/16"],
            AllowedHosts = ["packages.example.com"],
        };

        configuration.Apply(options);

        Assert.Equal(2, options.ForwardLimit);
        Assert.Contains(IPAddress.Parse("10.2.0.8"), options.KnownProxies);
        Assert.Contains(System.Net.IPNetwork.Parse("10.42.0.0/16"), options.KnownIPNetworks);
        Assert.Equal(["packages.example.com"], options.AllowedHosts);
    }

    [Theory]
    [InlineData("not-an-address", null)]
    [InlineData(null, "not-a-network")]
    [InlineData(null, "0.0.0.0/0")]
    public void Apply_RejectsInvalidOrUnboundedTrust(string? proxy, string? network)
    {
        var configuration = new HostReverseProxyOptions
        {
            KnownProxies = proxy is null ? [] : [proxy],
            KnownNetworks = network is null ? [] : [network],
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => configuration.Apply(new ForwardedHeadersOptions()));

        Assert.Contains(HostReverseProxyOptions.SectionName, exception.Message, StringComparison.Ordinal);
    }
}
