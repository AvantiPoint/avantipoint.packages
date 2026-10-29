using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Packages.Registry.Native.Extensions;
using AvantiPoint.Packages.Registry.Native;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeMavenLimitConfigurationTests
{
    [Fact]
    public void MavenArtifactLimitIsIndependentOfUnusedArchiveExpansionControls()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Feed:PublicBaseUrl"] = "https://registry.example.test/",
            ["Feed:Maven:MaxArtifactBytes"] = "4294967296",
            ["Feed:Maven:MaxExpandedArchiveBytes"] = "1",
            ["Feed:Maven:MaxArchiveEntries"] = "0",
        });
        var feed = builder.AddAvantiPointFeed(builder.Configuration.GetSection("Feed"));
        feed.UseNativeRegistry(FeedProtocol.Maven);
        using var services = builder.Services.BuildServiceProvider();
        var options = services.GetRequiredService<IOptionsMonitor<NativeRegistryOptions>>().Get("Maven");
        Assert.Equal(4294967296L, options.MaxArtifactBytes);
    }
}
