using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Admin.Services.Upstreams;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Upstreams;

public sealed class ConfigurationPackageSourceSeederTests
{
    [Fact]
    public async Task ApplyAsync_UpsertsEveryProtocol_AndIsIdempotent()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = await CreateContextAsync(connection);
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PackageSources:0:Name"] = "NuGet.org",
            ["PackageSources:0:FeedUrl"] = "https://api.nuget.org/v3/index.json",
            ["PackageSources:0:Protocol"] = "NuGet",
            ["PackageSources:0:CachingStrategy"] = "IndexAndCache",
            ["PackageSources:1:Name"] = "Private npm",
            ["PackageSources:1:FeedUrl"] = "https://npm.example.test",
            ["PackageSources:1:Protocol"] = "Npm",
            ["PackageSources:1:ApiKey"] = "npm-token",
            ["PackageSources:1:CachingStrategy"] = "CacheOnly",
            ["PackageSources:2:Name"] = "Docker Hub",
            ["PackageSources:2:FeedUrl"] = "https://registry-1.docker.io",
            ["PackageSources:2:Protocol"] = "Oci",
            ["PackageSources:2:Surface"] = " docker ",
            ["PackageSources:2:Username"] = "robot",
            ["PackageSources:2:Password"] = "secret",
            ["PackageSources:2:CachingStrategy"] = "ProxyOnly",
        });
        var seeder = CreateSeeder(configuration, context);

        await seeder.ApplyAsync(TestContext.Current.CancellationToken);
        await seeder.ApplyAsync(TestContext.Current.CancellationToken);

        var sources = await context.PackageSources
            .AsNoTracking()
            .OrderBy(source => source.Protocol)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, sources.Count);

        var npm = Assert.Single(sources, source => source.Protocol == PackageSourceProtocol.Npm);
        Assert.Equal(PackageSourceCachingStrategy.CacheOnly, npm.CachingStrategy);
        Assert.Equal("npm-token", npm.ApiKey);
        Assert.Null(npm.Surface);

        var oci = Assert.Single(sources, source => source.Protocol == PackageSourceProtocol.Oci);
        Assert.Equal("docker", oci.Surface);
        Assert.Equal("robot", oci.Username);
        Assert.Equal("secret", oci.Password);
        Assert.Equal(PackageSourceCachingStrategy.ProxyOnly, oci.CachingStrategy);
    }

    [Fact]
    public async Task ApplyAsync_UpdatesConfigurationFields_AndPreservesOmittedSecrets()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = await CreateContextAsync(connection);
        var packageSourceService = CreatePackageSourceService(context);
        await packageSourceService.AddAsync(
            new PackageSource
            {
                Name = "Private npm",
                FeedUrl = "https://old.example.test",
                Protocol = PackageSourceProtocol.Npm,
                ApiKey = "keep-me",
                Priority = 20,
            },
            TestContext.Current.CancellationToken);

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PackageSources:0:Name"] = "Private npm",
            ["PackageSources:0:FeedUrl"] = "https://new.example.test",
            ["PackageSources:0:Protocol"] = "Npm",
            ["PackageSources:0:Priority"] = "2",
            ["PackageSources:0:CachingStrategy"] = "ProxyOnly",
        });
        var seeder = CreateSeeder(configuration, context, packageSourceService);

        await seeder.ApplyAsync(TestContext.Current.CancellationToken);

        var source = await context.PackageSources.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("https://new.example.test", source.FeedUrl);
        Assert.Equal(2, source.Priority);
        Assert.Equal(PackageSourceCachingStrategy.ProxyOnly, source.CachingStrategy);
        Assert.Equal("keep-me", source.ApiKey);
    }

    [Fact]
    public async Task ApplyAsync_RejectsDuplicateProtocolAndName()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = await CreateContextAsync(connection);
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PackageSources:0:Name"] = "Registry",
            ["PackageSources:0:FeedUrl"] = "https://one.example.test",
            ["PackageSources:0:Protocol"] = "Oci",
            ["PackageSources:1:Name"] = "registry",
            ["PackageSources:1:FeedUrl"] = "https://two.example.test",
            ["PackageSources:1:Protocol"] = "Oci",
        });
        var seeder = CreateSeeder(configuration, context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.ApplyAsync(TestContext.Current.CancellationToken));

        Assert.Contains("more than one Oci source", exception.Message);
        Assert.Empty(context.PackageSources);
    }

    private static async Task<SqliteContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;
        var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return context;
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static ConfigurationPackageSourceSeeder CreateSeeder(
        IConfiguration configuration,
        SqliteContext context,
        IPackageSourceService? packageSourceService = null)
    {
        var service = packageSourceService ?? CreatePackageSourceService(context);
        return new ConfigurationPackageSourceSeeder(
            configuration,
            context,
            service,
            new NullSecretProtector(),
            NullLogger<ConfigurationPackageSourceSeeder>.Instance);
    }

    private static PackageSourceService CreatePackageSourceService(SqliteContext context) =>
        new(
            context,
            Options.Create(new MirrorOptions()),
            new NuGetConfigParser(Mock.Of<ILogger<NuGetConfigParser>>()),
            new NullSecretProtector());
}
