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
            Mock.Of<ILogger<PackageSourceService>>(),
            mirrorOptions,
            new NuGetConfigParser(Mock.Of<ILogger<NuGetConfigParser>>()));

        var sources = await service.GetEnabledUpstreamSourcesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, sources.Count);
        Assert.Equal(1, await context.PackageSources.CountAsync(TestContext.Current.CancellationToken));

        var configSource = sources.Single(s => s.Name == "contoso");
        Assert.Equal(PackageSourceType.Upstream, configSource.Type);
        Assert.Equal(MirrorRepositorySignaturePolicy.Resign, configSource.MirrorSignaturePolicy);
        Assert.Equal(PackageSourceCachingStrategy.IndexAndCache, configSource.CachingStrategy);
        Assert.Equal(0, configSource.Id);
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
