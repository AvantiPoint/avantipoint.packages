using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Database.MySql;
using AvantiPoint.Packages.Database.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AvantiPoint.Packages.Database.Tests;

public sealed class NativeIdentityModelTests
{
    [Theory]
    [InlineData("SqlServer", "Latin1_General_100_BIN2")]
    [InlineData("MySql", "utf8mb4_bin")]
    [InlineData("Sqlite", "BINARY")]
    [InlineData("PostgreSql", "C")]
    public void NativeUniqueIdentityUsesExactCaseCollations(string provider, string expected)
    {
        using DbContext context = provider switch
        {
            "SqlServer" => new SqlServerContext(new DbContextOptionsBuilder<SqlServerContext>().UseSqlServer("Server=localhost;Database=model").Options),
            "MySql" => new MySqlContext(new DbContextOptionsBuilder<MySqlContext>().UseMySQL("Server=localhost;Database=model").Options),
            "PostgreSql" => new PostgreSqlContext(new DbContextOptionsBuilder<PostgreSqlContext>().UseNpgsql("Host=localhost;Database=model").Options),
            _ => new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite("Data Source=:memory:").Options),
        };
        Assert.False(context.Database.HasPendingModelChanges());
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(NativeArtifact))!;
        Assert.Equal(expected, entity.FindProperty(nameof(NativeArtifact.FeedId))!.GetCollation());
        Assert.Equal(expected, entity.FindProperty(nameof(NativeArtifact.Protocol))!.GetCollation());
    }
}
