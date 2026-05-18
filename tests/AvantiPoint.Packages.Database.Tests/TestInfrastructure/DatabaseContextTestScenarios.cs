using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

/// <summary>
/// Provider-agnostic database tests executed against a migrated <see cref="AbstractContext"/>.
/// </summary>
internal static class DatabaseContextTestScenarios
{
    public static readonly string[] ExpectedViews =
    [
        "vw_PackageDownloadCounts",
        "vw_LatestPackageVersions",
        "vw_PackageSearchInfo",
        "vw_PackageVersionsWithDownloads",
        "vw_PackageWithJsonData",
    ];

    public static async Task CanMigrateAsync(AbstractContext context, CancellationToken cancellationToken)
    {
        Assert.NotNull(context.Packages);
        Assert.NotNull(context.PackageDependencies);
        Assert.NotNull(context.PackageDownloads);
        Assert.NotNull(context.PackageTypes);
        Assert.NotNull(context.TargetFrameworks);
        Assert.NotNull(context.RepositorySigningCertificates);
        Assert.NotNull(context.PackageSources);
        Assert.NotNull(context.VulnerabilityRecords);
        Assert.NotNull(context.PackageVulnerabilities);

        // Prove tables exist (catches migrations that skip or duplicate objects).
        await context.Packages.CountAsync(cancellationToken);
        await context.RepositorySigningCertificates.CountAsync(cancellationToken);
        await context.PackageSources.CountAsync(cancellationToken);
        await context.VulnerabilityRecords.CountAsync(cancellationToken);
        await context.PackageVulnerabilities.CountAsync(cancellationToken);
    }

    public static async Task CanInsertAndQueryTestDataAsync(AbstractContext context, CancellationToken cancellationToken)
    {
        var package = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0"),
            Authors = ["Test Author"],
            Description = "Test Description",
            Listed = true,
            Published = DateTime.UtcNow,
            Dependencies =
            [
                new PackageDependency { Id = "Dependency1", VersionRange = "[1.0.0,)" },
                new PackageDependency { Id = "Dependency2", VersionRange = "[2.0.0,)" }
            ],
            PackageTypes = [new PackageType { Name = "Dependency" }],
            TargetFrameworks = [new TargetFramework { Moniker = "net8.0" }]
        };

        context.Packages.Add(package);
        await context.SaveChangesAsync(cancellationToken);

        var retrieved = await context.Packages
            .Include(p => p.Dependencies)
            .Include(p => p.PackageTypes)
            .Include(p => p.TargetFrameworks)
            .FirstOrDefaultAsync(p => p.Id == "TestPackage", cancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("TestPackage", retrieved.Id);
        Assert.Equal(2, retrieved.Dependencies.Count);
        Assert.Single(retrieved.PackageTypes);
        Assert.Single(retrieved.TargetFrameworks);
    }

    public static async Task CanQueryWithIndexedColumnsAsync(AbstractContext context, CancellationToken cancellationToken)
    {
        context.Packages.AddRange(
            new Package
            {
                Id = "Listed",
                Version = NuGetVersion.Parse("1.0.0"),
                Authors = ["Author"],
                Description = "Listed package",
                Listed = true,
                IsPrerelease = false,
                Published = DateTime.UtcNow.AddDays(-1),
                SemVerLevel = SemVerLevel.SemVer2
            },
            new Package
            {
                Id = "Unlisted",
                Version = NuGetVersion.Parse("1.0.0"),
                Authors = ["Author"],
                Description = "Unlisted package",
                Listed = false,
                IsPrerelease = false,
                Published = DateTime.UtcNow.AddDays(-2),
                SemVerLevel = SemVerLevel.SemVer2
            },
            new Package
            {
                Id = "Prerelease",
                Version = NuGetVersion.Parse("2.0.0-beta"),
                Authors = ["Author"],
                Description = "Prerelease package",
                Listed = true,
                IsPrerelease = true,
                Published = DateTime.UtcNow,
                SemVerLevel = SemVerLevel.SemVer2
            });
        await context.SaveChangesAsync(cancellationToken);

        Assert.Equal(2, await context.Packages.Where(p => p.Listed).CountAsync(cancellationToken));
        Assert.Equal(1, await context.Packages.Where(p => p.IsPrerelease).CountAsync(cancellationToken));

        var orderedByPublished = await context.Packages
            .OrderByDescending(p => p.Published)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        Assert.Equal("Prerelease", orderedByPublished[0]);
    }

    public static async Task CanTrackPackageDownloadsAsync(AbstractContext context, CancellationToken cancellationToken)
    {
        var package = new Package
        {
            Id = "DownloadTest",
            Version = NuGetVersion.Parse("1.0.0"),
            Authors = ["Author"],
            Description = "Download test",
            Listed = true,
            Published = DateTime.UtcNow
        };
        context.Packages.Add(package);
        await context.SaveChangesAsync(cancellationToken);

        context.PackageDownloads.AddRange(
            new PackageDownload { PackageKey = package.Key },
            new PackageDownload { PackageKey = package.Key },
            new PackageDownload { PackageKey = package.Key });
        await context.SaveChangesAsync(cancellationToken);

        var downloadCount = await context.PackageDownloads
            .Where(d => d.PackageKey == package.Key)
            .CountAsync(cancellationToken);
        Assert.Equal(3, downloadCount);

        var downloadsByPackage = await context.PackageDownloads
            .GroupBy(d => d.PackageKey)
            .Select(g => new { PackageKey = g.Key, Count = g.Count() })
            .FirstOrDefaultAsync(g => g.PackageKey == package.Key, cancellationToken);

        Assert.NotNull(downloadsByPackage);
        Assert.Equal(3, downloadsByPackage.Count);
    }

    public static async Task ViewsExistAndAreQueryableAsync(
        AbstractContext context,
        ITestOutputHelper output,
        CancellationToken cancellationToken)
    {
        var package = new Package
        {
            Id = "ViewTest",
            Version = NuGetVersion.Parse("1.0.0"),
            Authors = ["Author"],
            Description = "View test",
            Listed = true,
            Published = DateTime.UtcNow,
            Dependencies = [new PackageDependency { Id = "Dep1", VersionRange = "[1.0.0,)" }]
        };
        context.Packages.Add(package);
        await context.SaveChangesAsync(cancellationToken);

        var viewPackage = await context.Set<PackageWithJsonData>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == "ViewTest", cancellationToken);

        Assert.NotNull(viewPackage);
        Assert.Equal("ViewTest", viewPackage.Id);
        Assert.NotNull(viewPackage.DependenciesJson);
        output.WriteLine($"DependenciesJson: {viewPackage.DependenciesJson}");
    }

    public static async Task IndexesExistAsync(
        AbstractContext context,
        DatabaseProviderKind provider,
        ITestOutputHelper output,
        CancellationToken cancellationToken)
    {
        var indexes = await context.Database.SqlQueryRaw<SchemaObjectName>(
                DatabaseSchemaQueries.PackageIndexNamesSql(provider))
            .ToListAsync(cancellationToken);

        var indexNames = indexes.Select(i => i.Name).ToList();
        output.WriteLine($"Found {indexNames.Count} indexes: {string.Join(", ", indexNames)}");

        Assert.Contains(indexNames, name => name.Contains("Listed", StringComparison.Ordinal));
        Assert.Contains(indexNames, name => name.Contains("IsPrerelease", StringComparison.Ordinal));
        Assert.Contains(indexNames, name => name.Contains("Published", StringComparison.Ordinal));
        Assert.Contains(indexNames, name => name.Contains("SemVerLevel", StringComparison.Ordinal));
    }

    public static async Task ViewsExistAsync(
        AbstractContext context,
        DatabaseProviderKind provider,
        ITestOutputHelper output,
        CancellationToken cancellationToken)
    {
        var views = await context.Database.SqlQueryRaw<SchemaObjectName>(
                DatabaseSchemaQueries.ViewNamesSql(provider))
            .ToListAsync(cancellationToken);

        var viewNames = views.Select(v => v.Name).ToList();
        output.WriteLine($"Found {viewNames.Count} views: {string.Join(", ", viewNames)}");

        foreach (var expected in ExpectedViews)
        {
            Assert.Contains(expected, viewNames);
        }
    }

    public static async Task CanUseSigningAndVulnerabilityTablesAsync(AbstractContext context, CancellationToken cancellationToken)
    {
        var source = new PackageSource
        {
            Name = "upstream",
            FeedUrl = "https://api.nuget.org/v3/index.json",
            Type = PackageSourceType.Upstream,
            CachingStrategy = PackageSourceCachingStrategy.IndexAndCache,
            MirrorSignaturePolicy = MirrorRepositorySignaturePolicy.Resign,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.PackageSources.Add(source);

        var record = new VulnerabilityRecord
        {
            AdvisoryUrl = "https://example.test/GHSA-test",
            Severity = "High",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        context.VulnerabilityRecords.Add(record);

        var certificate = new RepositorySigningCertificate
        {
            Fingerprint = new string('a', 64),
            HashAlgorithm = CertificateHashAlgorithm.Sha256,
            Subject = "CN=Test",
            Issuer = "CN=Test",
            NotBefore = DateTime.UtcNow.AddDays(-1),
            NotAfter = DateTime.UtcNow.AddYears(1),
            FirstUsed = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            IsActive = true
        };
        context.RepositorySigningCertificates.Add(certificate);

        await context.SaveChangesAsync(cancellationToken);

        var package = new Package
        {
            Id = "SigningSchemaTest",
            Version = NuGetVersion.Parse("1.0.0"),
            Authors = ["Author"],
            Description = "Signing schema test",
            Listed = true,
            Published = DateTime.UtcNow,
            Origin = PackageOrigin.Published,
            PackageSourceId = source.Id
        };
        context.Packages.Add(package);

        context.PackageVulnerabilities.Add(new PackageVulnerability
        {
            PackageId = package.Id,
            VersionRange = "[1.0.0,)",
            VulnerabilityKey = record.Key
        });

        await context.SaveChangesAsync(cancellationToken);

        Assert.Equal(1, await context.PackageSources.CountAsync(cancellationToken));
        Assert.Equal(1, await context.VulnerabilityRecords.CountAsync(cancellationToken));
        Assert.Equal(1, await context.RepositorySigningCertificates.CountAsync(cancellationToken));
        Assert.Equal(1, await context.PackageVulnerabilities.CountAsync(cancellationToken));
    }

    private sealed class SchemaObjectName
    {
        public string Name { get; set; } = string.Empty;
    }
}
