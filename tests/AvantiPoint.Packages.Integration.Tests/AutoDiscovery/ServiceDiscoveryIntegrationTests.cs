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

public sealed class ServiceDiscoveryIntegrationTests
{

    [Fact]
    public async Task AutoDiscoverSqlite_ResolvesSqliteContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"ap-auto-sqlite-{Guid.NewGuid():N}.db");
        var storagePath = NuGetHostTestHelper.CreateStoragePath();

        var configuration = new Dictionary<string, string?>
        {
            ["Database:Type"] = DatabaseProviderNames.Sqlite,
            ["Database:ConnectionString"] = $"Data Source={databasePath}",
            ["Storage:Type"] = StorageProviderNames.FileSystem,
            ["Storage:Path"] = storagePath
        };

        var host = await NuGetHostTestHelper.BuildHostAsync(configuration, options =>
        {
            options.AutoDiscoverSqliteDatabase();
            options.AutoDiscoverFileStorage();
        });

        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            var sqliteContext = Assert.IsType<SqliteContext>(context);
            await sqliteContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            Assert.True(await sqliteContext.Database.CanConnectAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await NuGetHostTestHelper.DisposeHostAsync(host);
            NuGetHostTestHelper.DeleteFile(databasePath);
            NuGetHostTestHelper.DeleteDirectory(storagePath);
        }
    }

    [Fact]
    public async Task ManualSqliteRegistration_IgnoresConfigurationType()
    {
        var storagePath = NuGetHostTestHelper.CreateStoragePath();
        var databasePath = Path.Combine(Path.GetTempPath(), $"ap-manual-sqlite-{Guid.NewGuid():N}.db");

        var configuration = new Dictionary<string, string?>
        {
            ["ValidateServiceProviders"] = bool.FalseString,
            ["Database:Type"] = DatabaseProviderNames.SqlServer,
            ["Database:ConnectionString"] = "Server=ignored;Database=ignored;",
            ["Storage:Type"] = StorageProviderNames.FileSystem,
            ["Storage:Path"] = storagePath
        };

        var host = await NuGetHostTestHelper.BuildHostAsync(configuration, options =>
        {
            options.AddSqliteDatabase(o => o.ConnectionString = $"Data Source={databasePath}");
            options.AddFileStorage();
        });

        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            Assert.IsType<SqliteContext>(context);
        }
        finally
        {
            await NuGetHostTestHelper.DisposeHostAsync(host);
            NuGetHostTestHelper.DeleteFile(databasePath);
            NuGetHostTestHelper.DeleteDirectory(storagePath);
        }
    }

    [Fact]
    public async Task AutoDiscoverFileStorage_ResolvesFileStorageService()
    {
        var storagePath = NuGetHostTestHelper.CreateStoragePath();
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"ap-auto-store-{Guid.NewGuid():N}.db");
        var configuration = new Dictionary<string, string?>
        {
            ["Database:Type"] = DatabaseProviderNames.Sqlite,
            ["Database:ConnectionString"] = $"Data Source={sqlitePath}",
            ["Storage:Type"] = StorageProviderNames.FileSystem,
            ["Storage:Path"] = storagePath
        };

        var host = await NuGetHostTestHelper.BuildHostAsync(configuration, options =>
        {
            options.AutoDiscoverSqliteDatabase();
            options.AutoDiscoverFileStorage();
        });

        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
            Assert.IsType<FileStorageService>(storage);
        }
        finally
        {
            await NuGetHostTestHelper.DisposeHostAsync(host);
            NuGetHostTestHelper.DeleteDirectory(storagePath);
            NuGetHostTestHelper.DeleteFile(sqlitePath);
        }
    }
}

