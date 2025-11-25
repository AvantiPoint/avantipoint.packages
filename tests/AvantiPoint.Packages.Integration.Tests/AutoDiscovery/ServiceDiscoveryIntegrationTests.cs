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

public sealed class SqlServerServiceDiscoveryIntegrationTests(SqlServerTestcontainerFixture fixture)
    : IClassFixture<SqlServerTestcontainerFixture>
{
    private readonly SqlServerTestcontainerFixture _fixture = fixture;

    [DockerFact]
    public async Task AutoDiscoverSqlServer_ResolvesSqlServerContext()
    {
        var storagePath = NuGetHostTestHelper.CreateStoragePath();
        var handle = await _fixture.CreateDatabaseAsync();

        var configuration = new Dictionary<string, string?>
        {
            ["Database:Type"] = DatabaseProviderNames.SqlServer,
            ["Database:ConnectionString"] = handle.ConnectionString,
            ["Storage:Type"] = StorageProviderNames.FileSystem,
            ["Storage:Path"] = storagePath
        };

        var host = await NuGetHostTestHelper.BuildHostAsync(configuration, options =>
        {
            options.AutoDiscoverSqlServerDatabase();
            options.AutoDiscoverFileStorage();
        });

        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            var sqlServerContext = Assert.IsType<SqlServerContext>(context);
            await sqlServerContext.Database.MigrateAsync();
            Assert.True(await sqlServerContext.Database.CanConnectAsync());
        }
        finally
        {
            await NuGetHostTestHelper.DisposeHostAsync(host);
            await _fixture.DropDatabaseAsync(handle.DatabaseName);
            NuGetHostTestHelper.DeleteDirectory(storagePath);
        }
    }
}

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

internal static class NuGetHostTestHelper
{
    public static async Task<IHost> BuildHostAsync(
        IDictionary<string, string?> configuration,
        Action<NuGetApiOptions> configureOptions)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddLogging();
        builder.Services.AddNuGetPackageApi(configureOptions);
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    public static async Task DisposeHostAsync(IHost host)
    {
        try
        {
            await host.StopAsync();
        }
        finally
        {
            host.Dispose();
        }
    }

    public static string CreateStoragePath()
    {
        var path = Path.Combine(Path.GetTempPath(), "ap-storage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    public static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

public sealed record SqlServerDatabaseHandle(string DatabaseName, string ConnectionString);

public sealed class SqlServerTestcontainerFixture : IAsyncLifetime
{
    private const string Password = "AvantiPoint#2025";
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword(Password)
        .WithEnvironment("ACCEPT_EULA", "1")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task<SqlServerDatabaseHandle> CreateDatabaseAsync()
    {
        var databaseName = $"Packages_{Guid.NewGuid():N}";

        await using var connection = new SqlConnection(GetMasterConnectionString());
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = databaseName
        };

        return new SqlServerDatabaseHandle(databaseName, builder.ConnectionString);
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new SqlConnection(GetMasterConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private string GetMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }
}

public sealed record PostgreSqlDatabaseHandle(string DatabaseName, string ConnectionString);

public sealed class PostgreSqlTestcontainerFixture : IAsyncLifetime
{
    private const string Username = "postgres";
    private const string Password = "AvantiPoint#2025";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername(Username)
        .WithPassword(Password)
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<PostgreSqlDatabaseHandle> CreateDatabaseAsync()
    {
        var databaseName = $"packages_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName
        };

        return new PostgreSqlDatabaseHandle(databaseName, builder.ConnectionString);
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await using (var terminate = connection.CreateCommand())
        {
            terminate.CommandText = $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{databaseName}'";
            await terminate.ExecuteNonQueryAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }
}

