using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using MySqlConnector;
using Testcontainers.MySql;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

public sealed class MySqlTestcontainerFixture(IMessageSink messageSink)
    : ContainerFixture<MySqlBuilder, MySqlContainer>(messageSink)
{
    private const string Username = "root";
    private const string Password = "AvantiPoint#2025";

    protected override MySqlBuilder Configure(MySqlBuilder builder)
    {
        return builder
            .WithImage("mysql:8.0")
            .WithUsername(Username)
            .WithPassword(Password)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("mysqld: ready for connections"));
    }

    public async Task<MySqlDatabaseHandle> CreateDatabaseAsync()
    {
        var databaseName = $"packages_{Guid.NewGuid():N}";

        var serverConnectionString = MySqlConnectionStrings.ConfigureForConnector(Container.GetConnectionString());
        await using var connection = new MySqlConnection(serverConnectionString);
        await OpenWithRetryAsync(connection);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE `{databaseName}`";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new MySqlConnectionStringBuilder(serverConnectionString)
        {
            Database = databaseName
        };

        return new MySqlDatabaseHandle(
            databaseName,
            MySqlConnectionStrings.ConfigureForEntityFramework(builder.ConnectionString));
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new MySqlConnection(MySqlConnectionStrings.ConfigureForConnector(Container.GetConnectionString()));
        await OpenWithRetryAsync(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{databaseName}`";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task OpenWithRetryAsync(MySqlConnection connection)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    return;
                }

                await connection.OpenAsync();
                return;
            }
            catch (MySqlException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }
}

public sealed record MySqlDatabaseHandle(string DatabaseName, string ConnectionString);
