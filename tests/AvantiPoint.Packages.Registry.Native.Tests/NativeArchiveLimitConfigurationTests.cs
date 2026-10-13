using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Packages.Registry.Native.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeArchiveLimitConfigurationTests
{
    [Theory]
    [InlineData(FeedProtocol.Swift, 17179869185L)]
    [InlineData(FeedProtocol.Pub, 17179869185L)]
    [InlineData(FeedProtocol.Swift, long.MaxValue)]
    [InlineData(FeedProtocol.Pub, long.MaxValue)]
    public void ExpandedLimitsFromDirectConfigurationRemainBounded(FeedProtocol protocol, long limit)
    {
        using var services = Configure(protocol, limit);
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptionsMonitor<NativeRegistryOptions>>()
            .Get(protocol.ToString()));
    }

    [Theory]
    [InlineData(FeedProtocol.Swift)]
    [InlineData(FeedProtocol.Pub)]
    public void ExpandedLimitAcceptsTheSupportedUpperBoundary(FeedProtocol protocol)
    {
        using var services = Configure(protocol, 17179869184L);
        Assert.Equal(17179869184L, services.GetRequiredService<IOptionsMonitor<NativeRegistryOptions>>()
            .Get(protocol.ToString()).MaxExpandedArchiveBytes);
    }

    private static ServiceProvider Configure(FeedProtocol protocol, long limit)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Feed:PublicBaseUrl"] = "https://registry.example.test/",
            [$"Feed:{protocol}:MaxExpandedArchiveBytes"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        builder.AddAvantiPointFeed(builder.Configuration.GetSection("Feed")).UseNativeRegistry(protocol);
        return builder.Services.BuildServiceProvider();
    }
}
