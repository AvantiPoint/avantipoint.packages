using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Storage;
using AvantiPoint.Packages.Database.PostgreSql;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Integration.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;
namespace AvantiPoint.Packages.Integration.Tests.AutoDiscovery;

public sealed class PostgreSqlServiceDiscoveryIntegrationTests(PostgreSqlTestcontainerFixture fixture)
    : IClassFixture<PostgreSqlTestcontainerFixture>
{
    private readonly PostgreSqlTestcontainerFixture _fixture = fixture;

    [DockerFact]
    public async Task AutoDiscoverPostgreSql_ResolvesPostgreSqlContext()
    {
        var storagePath = NuGetHostTestHelper.CreateStoragePath();
        var handle = await _fixture.CreateDatabaseAsync();

        var configuration = new Dictionary<string, string?>
        {
            ["Database:Type"] = DatabaseProviderNames.PostgreSql,
            ["Database:ConnectionString"] = handle.ConnectionString,
            ["Storage:Type"] = StorageProviderNames.FileSystem,
            ["Storage:Path"] = storagePath
        };

        var host = await NuGetHostTestHelper.BuildHostAsync(configuration, options =>
        {
            options.AutoDiscoverPostgreSqlDatabase();
            options.AutoDiscoverFileStorage();
        });

        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            var postgresContext = Assert.IsType<PostgreSqlContext>(context);
            await postgresContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            Assert.True(await postgresContext.Database.CanConnectAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await NuGetHostTestHelper.DisposeHostAsync(host);
            await _fixture.DropDatabaseAsync(handle.DatabaseName);
            NuGetHostTestHelper.DeleteDirectory(storagePath);
        }
    }
}

