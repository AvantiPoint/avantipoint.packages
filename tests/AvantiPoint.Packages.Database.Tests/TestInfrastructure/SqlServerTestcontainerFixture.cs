using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

public sealed class SqlServerTestcontainerFixture(IMessageSink messageSink)
    : DbContainerFixture<MsSqlBuilder, MsSqlContainer>(messageSink)
{
    private const string Password = "AvantiPoint#2025";
    public override DbProviderFactory DbProviderFactory => SqlClientFactory.Instance;

    protected override MsSqlBuilder Configure(MsSqlBuilder builder)
    {
        return builder
            .WithPassword(Password)
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "1");
    }

    public async Task<SqlServerDatabaseHandle> CreateDatabaseAsync()
    {
        var databaseName = $"Packages_{Guid.NewGuid():N}";
        var masterConnectionString = BuildMasterConnectionString();

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new SqlConnectionStringBuilder(Container.GetConnectionString())
        {
            InitialCatalog = databaseName
        };

        return new SqlServerDatabaseHandle(databaseName, builder.ConnectionString);
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new SqlConnection(BuildMasterConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;" +
            $" DROP DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private string BuildMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(Container.GetConnectionString())
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }
}

public sealed record SqlServerDatabaseHandle(string DatabaseName, string ConnectionString);

