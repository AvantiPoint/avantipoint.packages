using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Npm.Tests;

public class ConfigurableMirrorPolicyServiceTests
{
    [Fact]
    public void GetStrategy_UsesConfiguredNpmCachingStrategy()
    {
        var service = CreateService(
            new NpmFeedOptions
            {
                Mirror = new NpmMirrorOptions
                {
                    CachingStrategy = MirrorCachingStrategy.ProxyOnly,
                },
            },
            new OciFeedOptions());

        var strategy = service.GetStrategy(FeedProtocol.Npm);

        Assert.Equal(MirrorCachingStrategy.ProxyOnly, strategy);
    }

    [Fact]
    public void GetStrategy_UsesConfiguredOciCachingStrategy()
    {
        var service = CreateService(
            new NpmFeedOptions(),
            new OciFeedOptions
            {
                Mirror = new OciMirrorOptions
                {
                    CachingStrategy = MirrorCachingStrategy.CacheOnly,
                },
            });

        var strategy = service.GetStrategy(FeedProtocol.Oci);

        Assert.Equal(MirrorCachingStrategy.CacheOnly, strategy);
    }

    private static ConfigurableMirrorPolicyService CreateService(NpmFeedOptions npmOptions, OciFeedOptions ociOptions) =>
        new(
            Options.Create(new SearchOptions()),
            Options.Create(npmOptions),
            new TestOptionsMonitor<OciFeedOptions>(ociOptions));

    private sealed class TestOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue => currentValue;

        public TOptions Get(string? name) => currentValue;

        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
}
