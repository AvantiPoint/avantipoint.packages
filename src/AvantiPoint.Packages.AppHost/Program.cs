using AvantiPoint.Packages.AppHost;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var config = builder.Configuration.Get<HostConfiguration>() ?? new HostConfiguration();

var feed = builder.AddProject<Projects.AvantiPoint_Packages_Server>("package-feed");

// Configure database based on configuration
if (config.DatabaseProvider == "PostgreSQL")
{
    ConfigurePostgreSqlDatabase(feed);
}
else if (config.DatabaseProvider == "SqlServer")
{
    ConfigureSqlServerDatabase(feed);
}
else
{
    ConfigureSqliteDatabase(feed);
}

// Configure storage based on configuration
if (config.StorageProvider == "Azure")
{
    ConfigureAzureStorage(feed);
}
else if (config.StorageProvider == "S3")
{
    ConfigureS3Storage(feed);
}
else
{
    ConfigureLocalStorage(feed);
}

builder.Build().Run();

static void ConfigureLocalStorage(IResourceBuilder<ProjectResource> feed)
{
    feed.WithEnvironment("Storage__Type", "FileSystem")
        .WithEnvironment("Storage__Path", "App_Data");
}

static void ConfigureAzureStorage(IResourceBuilder<ProjectResource> feed)
{
    var storage = feed.ApplicationBuilder.AddAzureStorage("storage")
        .AddBlobContainer("packages")
        .ExcludeFromManifest();
    
    feed.WithEnvironment("Storage__Type", "AzureBlobStorage")
        .WithEnvironment("Storage__Container", "packages")
        .WithEnvironment("Storage__ConnectionStringName", "storage")
        .WithReference(storage)
        .WaitFor(storage);
}

static void ConfigureS3Storage(IResourceBuilder<ProjectResource> feed)
{
    var minio = feed.ApplicationBuilder.AddContainer("minio", "minio/minio")
        .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
        .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
        .WithBindMount("../minio-data", "/data")
        .WithEndpoint(9000, name: "api")
        .WithEndpoint(9001, name: "console")
        .WithArgs("server", "/data", "--console-address", ":9001")
        .ExcludeFromManifest();
    
    feed.WithEnvironment("Storage__Type", "AwsS3")
        .WithEnvironment("Storage__Bucket", "packages")
        .WithEnvironment("Storage__Region", "us-east-1")
        .WithEnvironment("Storage__AccessKey", "minioadmin")
        .WithEnvironment("Storage__SecretKey", "minioadmin")
        .WithEnvironment("Storage__ServiceUrl", minio.GetEndpoint("api"))
        .WithEnvironment("Storage__ForcePathStyle", "true")
        .WaitFor(minio);
}

static void ConfigureSqlServerDatabase(IResourceBuilder<ProjectResource> feed)
{
    var sql = feed.ApplicationBuilder.AddSqlServer("sqlServer")
        .WithDataVolume();

    var database = sql.AddDatabase("packageFeed");

    feed.WithEnvironment("Database__Type", "SqlServer")
        .WithEnvironment("Database__ConnectionString", database.Resource.ConnectionStringExpression)
        .WithReference(database)
        .WaitFor(database);
}

static void ConfigurePostgreSqlDatabase(IResourceBuilder<ProjectResource> feed)
{
    var postgres = feed.ApplicationBuilder.AddPostgres("postgres")
        .WithDataVolume();

    var database = postgres.AddDatabase("packageFeed");

    feed.WithEnvironment("Database__Type", "PostgreSql")
        .WithEnvironment("Database__ConnectionString", database.Resource.ConnectionStringExpression)
        .WithReference(database)
        .WaitFor(database);
}

static void ConfigureSqliteDatabase(IResourceBuilder<ProjectResource> feed)
{
    feed.WithEnvironment("Database__Type", "Sqlite")
        .WithEnvironment("Database__ConnectionString", "Data Source=App_Data/packages.db");
}

