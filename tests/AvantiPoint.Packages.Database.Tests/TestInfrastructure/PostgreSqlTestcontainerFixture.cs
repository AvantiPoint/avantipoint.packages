using System;
using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

public sealed class PostgreSqlTestcontainerFixture(IMessageSink messageSink)
    : DbContainerFixture<PostgreSqlBuilder, PostgreSqlContainer>(messageSink)
{
    private const string Username = "postgres";
    private const string Password = "AvantiPoint#2025";
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;

    [Obsolete("See Testcontainers.ContainerLifetime.Configure.")]
    protected override PostgreSqlBuilder Configure(PostgreSqlBuilder builder)
    {
        return builder
            .WithImage("postgres:16-alpine")
            .WithUsername(Username)
            .WithPassword(Password);
    }

    public async Task<PostgreSqlDatabaseHandle> CreateDatabaseAsync()
    {
        var databaseName = $"packages_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(Container.GetConnectionString());
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(Container.GetConnectionString())
        {
            Database = databaseName
        };

        return new PostgreSqlDatabaseHandle(databaseName, builder.ConnectionString);
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(Container.GetConnectionString());
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

public sealed record PostgreSqlDatabaseHandle(string DatabaseName, string ConnectionString);

