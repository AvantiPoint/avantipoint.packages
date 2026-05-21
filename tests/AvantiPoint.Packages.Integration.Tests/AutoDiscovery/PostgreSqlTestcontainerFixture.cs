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

public sealed class PostgreSqlTestcontainerFixture : IAsyncLifetime
{
    private const string Username = "postgres";
    private const string Password = "AvantiPoint#2025";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
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

