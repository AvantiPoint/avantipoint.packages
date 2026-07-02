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
    public async Task Npm_ReturnsEmpty_WhenAllDatabaseSourcesAreDisabled()
    {
        var sources = new Mock<IPackageSourceService>();
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Npm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        sources
            .Setup(s => s.HasUpstreamSourcesAsync(PackageSourceProtocol.Npm, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // a row exists; it's just disabled

        var options = Options.Create(new NpmFeedOptions
        {
            Mirror = new NpmMirrorOptions { RegistryUrl = "https://registry.npmjs.org" },
        });

        var provider = new DatabaseNpmUpstreamRegistryProvider(sources.Object, new NullSecretProtector(), options);
        var registries = await provider.GetRegistriesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(registries); // must NOT silently fall back to the default registry
    }

    [Fact]
    public async Task Oci_UsesDatabaseSources_AndUnprotectsCredentials()
    {
        var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
        var surface = CreateSurface();
        var sources = new Mock<IPackageSourceService>();
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, surface.OciSegment, It.IsAny<CancellationToken>()))
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

        var registries = await provider.GetRegistriesAsync(surface, TestContext.Current.CancellationToken);

        var registry = Assert.Single(registries);
        Assert.Equal("https://registry-1.docker.io", registry.Url);
        Assert.Equal("bob", registry.Username);
        Assert.Equal("hunter2", registry.Password);
    }

    [Fact]
    public async Task Oci_FallsBackToConfigurationProvider_WhenNoDatabaseSources()
    {
        var surface = CreateSurface();
        var sources = new Mock<IPackageSourceService>();
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, surface.OciSegment, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var fallback = new Mock<IOciUpstreamRegistryProvider>();
        fallback
            .Setup(f => f.GetRegistriesAsync(surface, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OciUpstreamRegistryOptions { Url = "https://mirror.gcr.io" }]);

        var provider = new DatabaseOciUpstreamRegistryProvider(sources.Object, new NullSecretProtector(), fallback.Object);
        var registries = await provider.GetRegistriesAsync(surface, TestContext.Current.CancellationToken);

        var registry = Assert.Single(registries);
        Assert.Equal("https://mirror.gcr.io", registry.Url);
    }

    [Fact]
    public async Task Oci_ReturnsEmpty_WhenAllDatabaseSourcesAreDisabled()
    {
        var surface = CreateSurface();
        var sources = new Mock<IPackageSourceService>();
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, surface.OciSegment, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        sources
            .Setup(s => s.HasUpstreamSourcesAsync(PackageSourceProtocol.Oci, surface.OciSegment, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // a row exists; it's just disabled

        var fallback = new Mock<IOciUpstreamRegistryProvider>(MockBehavior.Strict);
        var provider = new DatabaseOciUpstreamRegistryProvider(sources.Object, new NullSecretProtector(), fallback.Object);

        var registries = await provider.GetRegistriesAsync(surface, TestContext.Current.CancellationToken);

        Assert.Empty(registries); // must NOT silently fall back to static configuration
    }

    [Fact]
    public async Task Oci_ScopesRequestToTheRequestedSurface()
    {
        // Confirms the provider actually forwards surface.OciSegment through to the service,
        // rather than querying protocol-wide (which would leak sources across surfaces).
        var dockerSurface = CreateSurface("docker");
        var helmSurface = CreateSurface("helm");

        var sources = new Mock<IPackageSourceService>(MockBehavior.Strict);
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, "docker", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PackageSource { Name = "docker-only", FeedUrl = "https://registry-1.docker.io", Surface = "docker" }]);
        sources
            .Setup(s => s.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, "helm", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PackageSource { Name = "helm-only", FeedUrl = "https://charts.example.com", Surface = "helm" }]);

        var provider = new DatabaseOciUpstreamRegistryProvider(
            sources.Object,
            new NullSecretProtector(),
            Mock.Of<IOciUpstreamRegistryProvider>(MockBehavior.Strict));

        var dockerRegistries = await provider.GetRegistriesAsync(dockerSurface, TestContext.Current.CancellationToken);
        var helmRegistries = await provider.GetRegistriesAsync(helmSurface, TestContext.Current.CancellationToken);

        Assert.Equal("https://registry-1.docker.io", Assert.Single(dockerRegistries).Url);
        Assert.Equal("https://charts.example.com", Assert.Single(helmRegistries).Url);
    }

    private static SurfaceContext CreateSurface(string? ociSegment = null) =>
        new("default", FeedProtocol.Oci, "oci-default", ociSegment, "/v2", new Uri("https://packages.example.com"));
}
