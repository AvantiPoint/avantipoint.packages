using Aspire.Hosting.Azure;

namespace AvantiPoint.Packages.AppHost;

public static class DatabaseConfigurationExtensions
{
    extension(IResourceBuilder<ProjectResource> feed)
    {
        public IResourceBuilder<ProjectResource> ConfigureDatabase(string? databaseProvider, string databaseName)
        {
            return databaseProvider switch
            {
                "PostgreSQL" => ConfigurePostgreSqlDatabase(feed, databaseName),
                "SqlServer" => ConfigureSqlServerDatabase(feed, databaseName),
                _ => feed.ApplicationBuilder.ExecutionContext.IsPublishMode
                                ? throw new NotSupportedException("Sqlite is not supported in publish mode. Please use SqlServer or PostgreSQL instead.")
                                : ConfigureSqliteDatabase(feed),
            };
        }
    }

    private static IResourceBuilder<AzureSqlServerResource>? SqlResource;

    private static IResourceBuilder<ProjectResource> ConfigureSqlServerDatabase(IResourceBuilder<ProjectResource> feed, string databaseName)
    {
        SqlResource ??= feed.ApplicationBuilder.AddAzureSqlServer("sql-server")
            .RunAsContainer(c => c.WithDataVolume().WithLifetime(ContainerLifetime.Persistent));

        var database = SqlResource.AddDatabase(databaseName);

        return feed.WithEnvironment("Database__Type", "SqlServer")
            .WithEnvironment("Database__ConnectionStringName", "PackageData")
            .WithReference(database, "PackageData")
            .WaitFor(database);
    }

    private static IResourceBuilder<PostgresServerResource>? PostgresResource;
    private static IResourceBuilder<ProjectResource> ConfigurePostgreSqlDatabase(IResourceBuilder<ProjectResource> feed, string databaseName)
    {
        PostgresResource ??= feed.ApplicationBuilder.AddPostgres("postgres")
            .WithDataVolume();

        var database = PostgresResource.AddDatabase(databaseName);

        return feed.WithEnvironment("Database__Type", "PostgreSql")
            .WithEnvironment("Database__ConnectionStringName", "PackageData")
            .WithReference(database, "PackageData")
            .WaitFor(database);
    }

    private static IResourceBuilder<ProjectResource> ConfigureSqliteDatabase(IResourceBuilder<ProjectResource> feed)
    {
        return feed.WithEnvironment("Database__Type", "Sqlite")
            .WithEnvironment("Database__ConnectionString", "Data Source=App_Data/packages.db");
    }
}

