using Aspire.Hosting.Azure;

namespace AvantiPoint.Packages.AppHost;

public static class StorageConfigurationExtensions
{
    extension(IResourceBuilder<ProjectResource> feed)
    {
        public IResourceBuilder<ProjectResource> ConfigureStorage(string? storageProvider, string directory = "packages")
        {
            // Configure storage based on configuration
            return storageProvider switch
            {
                "Azure" => ConfigureAzureStorage(feed, directory),
                "S3" => ConfigureS3Storage(feed),
                _ => feed.ApplicationBuilder.ExecutionContext.IsPublishMode
                                ? throw new NotSupportedException("Local storage is not supported in publish mode. Please use Azure or S3 instead.")
                                : ConfigureLocalStorage(feed),
            };
        }
    }

    private static IResourceBuilder<ProjectResource> ConfigureLocalStorage(IResourceBuilder<ProjectResource> feed)
    {
        feed.WithEnvironment("Storage__Type", "FileSystem")
            .WithEnvironment("Storage__Path", "App_Data");
        return feed;
    }

    private const string PackageStorage = "PackageStorage";
    private static IResourceBuilder<AzureStorageResource>? AzureStorageResource;
    private static IResourceBuilder<ProjectResource> ConfigureAzureStorage(IResourceBuilder<ProjectResource> feed, string container)
    {
        AzureStorageResource ??= feed.ApplicationBuilder.AddAzureStorage("feed-storage")
            .RunAsEmulator();
        var packages = AzureStorageResource
            .AddBlobContainer(container);

        feed.WithEnvironment("Storage__Type", "AzureBlobStorage")
            .WithEnvironment("Storage__Container", "packages")
            .WithEnvironment("Storage__ConnectionStringName", PackageStorage)
            .WithReference(packages, PackageStorage)
            .WaitFor(packages);

        return feed;
    }

    private static IResourceBuilder<ContainerResource>? MiniIOResource;
    private static IResourceBuilder<ProjectResource> ConfigureS3Storage(IResourceBuilder<ProjectResource> feed)
    {
        var storageRegion = feed.ApplicationBuilder.AddParameter("storage-region", "us-east-1");

        feed.WithEnvironment("Storage__Type", "AwsS3")
            .WithEnvironment("Storage__Bucket", "packages")
            .WithEnvironment("Storage__Region", storageRegion);

        if (feed.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var s3 = feed.ApplicationBuilder.AddConnectionString("s3-connectionstring");
            feed.WithReference(s3, "S3Storage")
                .WithEnvironment("Storage__ConnectionStringName", "S3Storage");
            return feed;
        }

        MiniIOResource ??= feed.ApplicationBuilder.AddContainer("minio", "minio/minio")
            .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
            .WithBindMount("../minio-data", "/data")
            .WithEndpoint(9000, name: "api")
            .WithEndpoint(9001, name: "console")
            .WithArgs("server", "/data", "--console-address", ":9001")
            .ExcludeFromManifest();

        feed
            .WithEnvironment("Storage__AccessKey", "minioadmin")
            .WithEnvironment("Storage__SecretKey", "minioadmin")
            .WithEnvironment("Storage__ServiceUrl", MiniIOResource.GetEndpoint("api"))
            .WithEnvironment("Storage__ForcePathStyle", "true")
            .WaitFor(MiniIOResource);

        return feed;
    }
}

