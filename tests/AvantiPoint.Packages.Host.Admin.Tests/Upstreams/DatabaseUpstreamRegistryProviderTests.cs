using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Services.Secrets;
using AvantiPoint.Packages.Host.Admin.Services.Upstreams;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Upstreams;

public sealed class DatabaseUpstreamRegistryProviderTests
{
    [Fact]
    public async Task Npm_UsesDatabaseSources_AndUnprotectsCredentials()
    {
        var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
        var sources = new Mock<IPackageSourceService>();
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Npm, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PackageSource
                {
                    Name = "fontawesome",
                    FeedUrl = "https://npm.fontawesome.com",
                    Protocol = PackageSourceProtocol.Npm,
                    ApiKey = protector.Protect("fa-token"),
                    Priority = 1,
                },
            ]);

        var options = Options.Create(new NpmFeedOptions
        {
            Mirror = new NpmMirrorOptions { RegistryUrl = "https://registry.npmjs.org" },
        });

        var provider = new DatabaseNpmUpstreamRegistryProvider(sources.Object, protector, options);
        var registries = await provider.GetRegistriesAsync(TestContext.Current.CancellationToken);

        var registry = Assert.Single(registries);
        Assert.Equal("https://npm.fontawesome.com", registry.Url);
        Assert.Equal("fa-token", registry.Token); // decrypted
        Assert.Equal(1, registry.Priority);
    }

    [Fact]
    public async Task Npm_FallsBackToConfiguration_WhenNoDatabaseSources()
    {
        var sources = new Mock<IPackageSourceService>();
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Npm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var options = Options.Create(new NpmFeedOptions
        {
            Mirror = new NpmMirrorOptions { RegistryUrl = "https://registry.npmjs.org" },
        });

        var provider = new DatabaseNpmUpstreamRegistryProvider(sources.Object, new NullSecretProtector(), options);
        var registries = await provider.GetRegistriesAsync(TestContext.Current.CancellationToken);

        var registry = Assert.Single(registries);
        Assert.Equal("https://registry.npmjs.org", registry.Url);
        Assert.Null(registry.Token);
    }

    [Fact]
    public async Task Oci_UsesDatabaseSources_AndUnprotectsCredentials()
    {
        var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
        var sources = new Mock<IPackageSourceService>();
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PackageSource
                {
                    Name = "docker-hub",
                    FeedUrl = "https://registry-1.docker.io",
                    Protocol = PackageSourceProtocol.Oci,
                    Username = protector.Protect("bob"),
                    Password = protector.Protect("hunter2"),
                },
            ]);

        var fallback = new Mock<IOciUpstreamRegistryProvider>(MockBehavior.Strict);
        var provider = new DatabaseOciUpstreamRegistryProvider(sources.Object, protector, fallback.Object);

        var registries = await provider.GetRegistriesAsync(CreateSurface(), TestContext.Current.CancellationToken);

        var registry = Assert.Single(registries);
        Assert.Equal("https://registry-1.docker.io", registry.Url);
        Assert.Equal("bob", registry.Username);
        Assert.Equal("hunter2", registry.Password);
    }

    [Fact]
    public async Task Oci_FallsBackToConfigurationProvider_WhenNoDatabaseSources()
    {
        var sources = new Mock<IPackageSourceService>();
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var surface = CreateSurface();
        var fallback = new Mock<IOciUpstreamRegistryProvider>();
        fallback
            .Setup(f => f.GetRegistriesAsync(surface, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OciUpstreamRegistryOptions { Url = "https://mirror.gcr.io" }]);

        var provider = new DatabaseOciUpstreamRegistryProvider(sources.Object, new NullSecretProtector(), fallback.Object);
        var registries = await provider.GetRegistriesAsync(surface, TestContext.Current.CancellationToken);

        var registry = Assert.Single(registries);
        Assert.Equal("https://mirror.gcr.io", registry.Url);
    }

    private static SurfaceContext CreateSurface() =>
        new("default", FeedProtocol.Oci, "oci-default", null, "/v2", new Uri("https://packages.example.com"));
}
