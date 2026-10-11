using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Database.Tests;

internal static class NativeCatalogCollationAssertions
{
    public static async Task VerifyAsync(IContext context, CancellationToken ct)
    {
        // Real provider defaults are commonly case-insensitive. Force that
        // condition here so a runner's database configuration cannot hide it.
        if (context.Database.ProviderName!.Contains("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE NativeArtifacts MODIFY PackageName varchar(256) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL", ct);
        }
        else if (context.Database.IsSqlServer())
        {
            await context.Database.ExecuteSqlRawAsync(
                "DROP INDEX IX_NativeArtifacts_FeedId_Protocol_PackageName ON NativeArtifacts", ct);
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE NativeArtifacts ALTER COLUMN PackageName nvarchar(256) COLLATE Latin1_General_100_CI_AS NOT NULL", ct);
            await context.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IX_NativeArtifacts_FeedId_Protocol_PackageName ON NativeArtifacts (FeedId, Protocol, PackageName)", ct);
        }

        const string feedId = "catalog";
        var expected = Enumerable.Range(0, 49).Select(i => $"com.example:aaa_{i:D2}")
            .Concat(["com.example:SDK", "com.example:sdk"]).ToArray();
        foreach (var name in expected)
            Add(context, feedId, "Maven", name, "1.0.0", name.EndsWith(":SDK") ? 123 : 456);
        Add(context, feedId, "Maven", "com.example:SDK", "2.0.0", 123);
        Add(context, "CATALOG", "Maven", "com.example:shadow", "9.0.0", 987654);
        Add(context, feedId, "maven", "com.example:lower_protocol", "9.0.0", 987654);
        await context.SaveChangesAsync(ct);
        if (context.Database.IsSqlServer() || context.Database.ProviderName!.Contains("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            var defaultNames = await context.NativeArtifacts.Where(artifact => artifact.FeedId == feedId && artifact.Protocol == "Maven")
                .Select(artifact => artifact.PackageName).Distinct().ToListAsync(ct);
            // Calibration: the provider really collapses the two names without
            // an explicit identity collation. The service must preserve both.
            Assert.Single(defaultNames, name => name.Equals("com.example:sdk", StringComparison.OrdinalIgnoreCase));
        }

        var registry = new FeedRegistry(new(feedId, "Catalog", string.Empty));
        registry.Register(new("maven", FeedProtocol.Maven, null, "/maven", "Feed:Maven"));
        using var services = new ServiceCollection().AddOptions()
            .Configure<FeedOptions>(options =>
            {
                options.PublicBaseUrl = "https://registry.test/prefix/";
                options.Authentication.AllowAnonymousPull = true;
            }).Configure<PackageFeedOptions>(_ => { }).BuildServiceProvider();
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var options = services.GetRequiredService<IOptionsMonitor<FeedOptions>>();
        var urls = new PublicBaseUrlProvider(options, services.GetRequiredService<IOptionsMonitor<PackageFeedOptions>>());
        var browse = new NativePackageBrowseService(context, registry, http, urls,
            services.GetRequiredService<IOptions<FeedOptions>>());
        var first = await browse.SearchPageAsync("maven", null, 1, ct);
        var second = await browse.SearchPageAsync("maven", null, 2, ct);
        var names = first.Packages.Concat(second.Packages).Select(package => package.Name).ToArray();
        Assert.Contains("com.example:SDK", names);
        Assert.Contains("com.example:sdk", names);
        Assert.Equal(expected.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
        Assert.Equal(50, first.Packages.Count);
        Assert.True(first.HasNextPage);
        Assert.Single(second.Packages);
        Assert.False(second.HasNextPage);
        Assert.Equal(51, names.Distinct(StringComparer.Ordinal).Count());
        var searched = await browse.SearchAsync("maven", "Sdk", ct);
        Assert.Equal(2, searched.Count);
        var upper = await browse.GetPackageAsync("maven", "com.example:SDK", ct);
        var lower = await browse.GetPackageAsync("maven", "com.example:sdk", ct);
        Assert.Equal(["2.0.0", "1.0.0"], upper!.Versions.Select(version => version.Version));
        Assert.All(upper.Versions.SelectMany(version => version.Artifacts), artifact => Assert.Equal(123, artifact.Length));
        Assert.Equal("1.0.0", Assert.Single(lower!.Versions).Version);
        Assert.All(lower.Versions.SelectMany(version => version.Artifacts), artifact => Assert.Equal(456, artifact.Length));
        Assert.Null(await browse.GetPackageAsync("maven", "com.example:shadow", ct));
        Assert.Null(await browse.GetPackageAsync("maven", "com.example:lower_protocol", ct));
    }

    private static void Add(IContext context, string feedId, string protocol, string name, string version, long length)
    {
        var identity = name.Split(':');
        var path = $"{identity[0].Replace('.', '/')}/{identity[1]}/{version}/{identity[1]}-{version}.pom";
        context.NativeArtifacts.Add(new NativeArtifact
        {
            FeedId = feedId, Protocol = protocol, PackageName = name, Version = version,
            Path = path, PathHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path))),
            ContentHash = new string('a', 64), ContentType = "application/xml", Length = length,
            MetadataJson = "{}", PublishedUtc = DateTime.UtcNow,
        });
    }
}
