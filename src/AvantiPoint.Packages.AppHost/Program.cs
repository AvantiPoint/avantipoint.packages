using AvantiPoint.Packages.AppHost;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var databaseProviderParameter = builder.AddParameter("database-provider", "SqlServer");
var databaseProvider = await databaseProviderParameter.Resource.GetValueAsync(default);
var storageProviderParameter = builder.AddParameter("storage-provider", "Azure");
var storageProvider = await storageProviderParameter.Resource.GetValueAsync(default);

//var feed = builder.AddProject<Projects.AvantiPoint_Packages_Server>("package-feed")
//    .ConfigureDatabase(databaseProvider, "feed-database")
//    .ConfigureStorage(storageProvider, "feed-storage")
//    .ExcludeFromManifest();

var host = builder.AddProject<Projects.AvantiPoint_Packages_Host>("package-host")
    .ConfigureDatabase(databaseProvider, "host-database")
    .ConfigureStorage(storageProvider, "host-storage");

builder.Build().Run();
