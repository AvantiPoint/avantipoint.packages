using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AvantiPoint.Packages.Database.Tests;

public sealed class NativeCallbackMigrationTests
{
    [Fact]
    public async Task ReceiptMigrationPreservesExistingCatalogRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync("20261006143916_AddNativeArtifacts", TestContext.Current.CancellationToken);
        var id = Guid.NewGuid().ToString("N");
        var hash = new string('a', 64);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO NativeArtifacts (Id, FeedId, Protocol, Path, PathHash, ContentHash, ContentType, Length, PackageName, Version, MetadataJson, PublishedUtc)
            VALUES ({id}, {"default"}, {"Pub"}, {"packages/sdk/versions/1.0.0.tar.gz"}, {hash}, {hash}, {"application/gzip"}, {123L}, {"sdk"}, {"1.0.0"}, {"{}"}, {DateTime.UtcNow})
            """, TestContext.Current.CancellationToken);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var artifact = await context.NativeArtifacts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(id, artifact.Id);
        Assert.Equal(hash, artifact.ContentHash);
        Assert.Equal(123, artifact.Length);
        Assert.Equal("sdk", artifact.PackageName);
        Assert.False(artifact.UploadCallbackCompleted);
        Assert.Null(artifact.UploadCallbackOwner);
        Assert.Null(artifact.UploadCallbackLeaseUntilUtc);
    }
}
