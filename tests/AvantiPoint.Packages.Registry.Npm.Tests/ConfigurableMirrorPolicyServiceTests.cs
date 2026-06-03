using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Npm;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public void NpmMirrorService_MapsCacheOnlyMirrorsToCachedOrigin()
    {
        var options = new NpmFeedOptions
        {
            Mirror = new NpmMirrorOptions
            {
                CachingStrategy = MirrorCachingStrategy.CacheOnly,
            },
        };
        var service = new NpmMirrorService(
            Options.Create(options),
            CreateService(options, new OciFeedOptions()),
            new SingleClientHttpClientFactory(new HttpClient()),
            NullLogger<NpmMirrorService>.Instance);

        Assert.Equal(MirrorCachingStrategy.CacheOnly, service.Strategy);
        Assert.Equal(PackageOrigin.Cached, service.MirrorOrigin);
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

    private sealed class SingleClientHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
