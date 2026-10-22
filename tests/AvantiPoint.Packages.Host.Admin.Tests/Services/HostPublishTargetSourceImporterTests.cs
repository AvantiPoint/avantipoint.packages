using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Secrets;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Admin.Tests.Services;

public sealed class HostPublishTargetSourceImporterTests
{
    [Theory]
    [InlineData(PackageSourceProtocol.NuGet, PublishTargetProtocol.NuGet, "https://registry.example/v3/index.json")]
    [InlineData(PackageSourceProtocol.Npm, PublishTargetProtocol.Npm, "https://registry.example/npm")]
    [InlineData(PackageSourceProtocol.Oci, PublishTargetProtocol.Oci, "https://registry.example/v2")]
    public async Task ExplicitImport_EncryptsCredentialAndCreatesNoAutomaticPublishingLink(PackageSourceProtocol sourceProtocol, PublishTargetProtocol targetProtocol, string url)
    {
        await using var catalogConnection = new SqliteConnection("Data Source=:memory:");
        await using var identityConnection = new SqliteConnection("Data Source=:memory:");
        await catalogConnection.OpenAsync(TestContext.Current.CancellationToken);
        await identityConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var packages = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(catalogConnection).Options);
        await using var identity = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(identityConnection).Options);
        await packages.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await identity.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var secrets = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
        var source = new PackageSource
        {
            Name = "Local import test", Protocol = sourceProtocol, FeedUrl = url,
            ApiKey = sourceProtocol == PackageSourceProtocol.Oci ? null : secrets.Protect("test-publish-secret"),
            Password = secrets.Protect("test-publish-secret"), Username = secrets.Protect("test-robot"),
        };
        packages.PackageSources.Add(source);
        await packages.SaveChangesAsync(TestContext.Current.CancellationToken);
        var importer = new HostPublishTargetSourceImporter(packages, identity, secrets);
        Assert.True((await importer.ImportAsync(source.Id, "admin@example.test", TestContext.Current.CancellationToken)).Succeeded);
        var target = await identity.HostPublishTargets.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(targetProtocol, target.Protocol);
        Assert.Equal(sourceProtocol == PackageSourceProtocol.Oci ? "https://registry.example" : url, target.PublishEndpoint);
        Assert.NotEqual("test-publish-secret", target.ApiToken);
        Assert.Equal("test-publish-secret", secrets.Unprotect(target.ApiToken));
        Assert.Empty(identity.HostPackageGroupSyndications);
        Assert.Single(identity.HostAuditEvents);
        Assert.DoesNotContain("test-publish-secret", (await identity.HostAuditEvents.SingleAsync(TestContext.Current.CancellationToken)).Detail);
        Assert.True((await importer.ImportAsync(source.Id, "admin@example.test", TestContext.Current.CancellationToken)).Succeeded);
        Assert.Single(identity.HostPublishTargets);
        Assert.Single(identity.HostAuditEvents);
        target.PublishEndpoint = "https://different.example.test";
        await identity.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False((await importer.ImportAsync(source.Id, "admin@example.test", TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal("https://different.example.test", target.PublishEndpoint);
    }
}
