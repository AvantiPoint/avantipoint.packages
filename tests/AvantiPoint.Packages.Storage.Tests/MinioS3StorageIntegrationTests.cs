using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using AvantiPoint.Packages.Aws;
using AvantiPoint.Packages.Storage.Tests.TestInfrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests;

[Collection("StorageIntegration")]
public sealed class MinioS3StorageIntegrationTests : IAsyncLifetime
{
    private const string BucketName = "packages";
    private IContainer? _container;
    private S3StorageService? _storage;

    [DockerFact]
    public async Task PutGetListDelete_RoundTrip()
    {
        Assert.NotNull(_storage);
        await StorageRoundTrip.ExecuteAsync(_storage);
    }

    public async ValueTask InitializeAsync()
    {
        _container = new ContainerBuilder("minio/minio")
            .WithCommand("server", "/data")
            .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
            .WithPortBinding(9000, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(9000).ForPath("/minio/health/live")))
            .Build();

        await _container.StartAsync();

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(9000);
        var serviceUrl = $"http://{host}:{port}";

        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };

        var client = new AmazonS3Client(
            new BasicAWSCredentials("minioadmin", "minioadmin"),
            s3Config);

        await client.PutBucketAsync(BucketName);

        var options = new S3StorageOptions
        {
            Bucket = BucketName,
            Region = "us-east-1",
            ServiceUrl = serviceUrl,
            ForcePathStyle = true,
            AccessKey = "minioadmin",
            SecretKey = "minioadmin"
        };

        _storage = new S3StorageService(new TestOptionsSnapshot<S3StorageOptions>(options), client);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
