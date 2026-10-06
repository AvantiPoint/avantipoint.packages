using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using AvantiPoint.Packages.Aws;
using AvantiPoint.Packages.Storage.Tests.TestInfrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests;

[Collection("StorageIntegration")]
public sealed class MinioS3StorageIntegrationTests : IAsyncLifetime
{
    private const string BucketName = "packages";
    private IFutureDockerImage? _image;
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
        var fixtureDirectory = Path.Combine(
            CommonDirectoryPath.GetSolutionDirectory(typeof(MinioS3StorageIntegrationTests).Assembly.Location).DirectoryPath,
            "tests", "AvantiPoint.Packages.Storage.Tests", "TestAssets", "Minio");
        _image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(fixtureDirectory)
            .WithBuildArgument("RESOURCE_REAPER_SESSION_ID", ResourceReaper.DefaultSessionId.ToString("D"))
            .WithCreateParameterModifier(parameters => parameters.Memory = 4L * 1024 * 1024 * 1024)
            .WithCleanUp(true)
            .Build();
        using (var buildTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
        {
            await _image.CreateAsync(buildTimeout.Token);
        }

        var password = Guid.NewGuid().ToString("N");
        _container = new ContainerBuilder(_image)
            .WithImagePullPolicy(PullPolicy.Never)
            .WithCommand("server", "/data")
            .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", password)
            .WithPortBinding(9000, assignRandomHostPort: true)
            .WithCreateParameterModifier(parameters =>
                parameters.HostConfig!.PortBindings["9000/tcp"][0].HostIP = "127.0.0.1")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(9000).ForPath("/minio/health/live")))
            .Build();

        using (var startupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
        {
            await _container.StartAsync(startupTimeout.Token);
        }

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
            new BasicAWSCredentials("minioadmin", password),
            s3Config);

        await client.PutBucketAsync(BucketName);

        var options = new S3StorageOptions
        {
            Bucket = BucketName,
            Region = "us-east-1",
            ServiceUrl = serviceUrl,
            ForcePathStyle = true,
            AccessKey = "minioadmin",
            SecretKey = password
        };

        _storage = new S3StorageService(new TestOptionsSnapshot<S3StorageOptions>(options), client);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_container != null)
            {
                await _container.DisposeAsync();
            }
        }
        finally
        {
            if (_image != null)
            {
                await _image.DisposeAsync();
            }
        }
    }
}
