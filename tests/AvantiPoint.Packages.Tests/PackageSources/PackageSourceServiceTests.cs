using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AvantiPoint.Packages.Tests.PackageSources;

public class PackageSourceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _tempDir;

    public PackageSourceServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _tempDir = Path.Combine(Path.GetTempPath(), $"pkg-source-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task GetEnabledUpstreamSourcesAsync_AppendsNuGetConfigSourcesWithoutPersisting()
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.PackageSources.Add(new PackageSource
        {
            Name = "nuget.org",
            FeedUrl = "https://api.nuget.org/v3/index.json",
            Type = PackageSourceType.Upstream,
            IsEnabled = true
        });
        await context.SaveChangesAsync();

        var configPath = Path.Combine(_tempDir, "NuGet.config");
        File.WriteAllText(configPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""contoso"" value=""https://packages.contoso.com/v3/index.json"" />
  </packageSources>
</configuration>");

        var mirrorOptions = Options.Create(new MirrorOptions
        {
            NuGetConfigPath = configPath,
            DefaultSignaturePolicy = MirrorRepositorySignaturePolicy.Resign,
            DefaultCachingStrategy = PackageSourceCachingStrategy.IndexAndCache
        });

        var service = new PackageSourceService(
            context,
            mirrorOptions,
            new NuGetConfigParser(Mock.Of<ILogger<NuGetConfigParser>>()),
            new NullSecretProtector());

        var sources = await service.GetEnabledUpstreamSourcesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, sources.Count);
        Assert.Equal(1, await context.PackageSources.CountAsync(TestContext.Current.CancellationToken));

        var configSource = sources.Single(s => s.Name == "contoso");
        Assert.Equal(PackageSourceType.Upstream, configSource.Type);
        Assert.Equal(MirrorRepositorySignaturePolicy.Resign, configSource.MirrorSignaturePolicy);
        Assert.Equal(PackageSourceCachingStrategy.IndexAndCache, configSource.CachingStrategy);
        Assert.Equal(0, configSource.Id);
    }

    [Fact]
    public async Task HasUpstreamSourcesAsync_IsTrue_EvenWhenAllMatchingSourcesAreDisabled()
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.PackageSources.Add(new PackageSource
        {
            Name = "fontawesome",
            FeedUrl = "https://npm.fontawesome.com",
            Protocol = PackageSourceProtocol.Npm,
            Type = PackageSourceType.Upstream,
            IsEnabled = false, // disabled, not absent
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PackageSourceService(
            context,
            Options.Create(new MirrorOptions()),
            new NuGetConfigParser(Mock.Of<ILogger<NuGetConfigParser>>()),
            new NullSecretProtector());

        var enabled = await service.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Npm, TestContext.Current.CancellationToken);
        var hasAny = await service.HasUpstreamSourcesAsync(PackageSourceProtocol.Npm, TestContext.Current.CancellationToken);

        Assert.Empty(enabled);
        Assert.True(hasAny); // distinguishes "disabled" from "never configured"
    }

    [Fact]
    public async Task HasUpstreamSourcesAsync_IsFalse_WhenNoSourcesExistForProtocol()
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var service = new PackageSourceService(
            context,
            Options.Create(new MirrorOptions()),
            new NuGetConfigParser(Mock.Of<ILogger<NuGetConfigParser>>()),
            new NullSecretProtector());

        Assert.False(await service.HasUpstreamSourcesAsync(PackageSourceProtocol.Oci, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetEnabledUpstreamSourcesAsync_ScopesBySurface_UnscopedSourceAppliesToEverySurface()
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.PackageSources.AddRange(
            new PackageSource
            {
                Name = "docker-only",
                FeedUrl = "https://registry-1.docker.io",
                Protocol = PackageSourceProtocol.Oci,
                Surface = "docker",
            },
            new PackageSource
            {
                Name = "helm-only",
                FeedUrl = "https://charts.example.com",
                Protocol = PackageSourceProtocol.Oci,
                Surface = "helm",
            },
            new PackageSource
            {
                Name = "applies-everywhere",
                FeedUrl = "https://mirror.example.com",
                Protocol = PackageSourceProtocol.Oci,
                Surface = null,
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PackageSourceService(
            context,
            Options.Create(new MirrorOptions()),
            new NuGetConfigParser(Mock.Of<ILogger<NuGetConfigParser>>()),
            new NullSecretProtector());

        var dockerSources = await service.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, "docker", TestContext.Current.CancellationToken);
        var helmSources = await service.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, "helm", TestContext.Current.CancellationToken);
        var defaultSources = await service.GetEnabledUpstreamSourcesAsync(PackageSourceProtocol.Oci, surface: null, TestContext.Current.CancellationToken);

        Assert.Equal(["applies-everywhere", "docker-only"], dockerSources.Select(s => s.Name).OrderBy(n => n));
        Assert.Equal(["applies-everywhere", "helm-only"], helmSources.Select(s => s.Name).OrderBy(n => n));
        Assert.Equal(["applies-everywhere"], defaultSources.Select(s => s.Name));
    }

    [Fact]
    public async Task UpdateAsync_PersistsProtocolPriorityAndSurface_ForADetachedSource()
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.PackageSources.Add(new PackageSource
        {
            Id = 1,
            Name = "source",
            FeedUrl = "https://example.com",
            Protocol = PackageSourceProtocol.NuGet,
            Priority = 0,
            Surface = null,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PackageSourceService(
            context,
            Options.Create(new MirrorOptions()),
            new NuGetConfigParser(Mock.Of<ILogger<NuGetConfigParser>>()),
            new NullSecretProtector());

        // A detached instance, as an external caller (not the admin page's tracked entity) would pass.
        var detached = new PackageSource
        {
            Id = 1,
            Name = "source",
            FeedUrl = "https://example.com",
            Protocol = PackageSourceProtocol.Oci,
            Priority = 5,
            Surface = "docker",
        };

        await service.UpdateAsync(detached, TestContext.Current.CancellationToken);

        var persisted = await context.PackageSources.AsNoTracking()
            .SingleAsync(s => s.Id == 1, TestContext.Current.CancellationToken);
        Assert.Equal(PackageSourceProtocol.Oci, persisted.Protocol);
        Assert.Equal(5, persisted.Priority);
        Assert.Equal("docker", persisted.Surface);
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
