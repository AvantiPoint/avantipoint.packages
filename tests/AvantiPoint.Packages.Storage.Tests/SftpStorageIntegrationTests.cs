using AvantiPoint.Packages.Sftp;
using AvantiPoint.Packages.Sftp.Storage;
using AvantiPoint.Packages.Storage.Tests.TestInfrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests;

[Collection("StorageIntegration")]
public sealed class SftpStorageIntegrationTests : IAsyncLifetime
{
    private IContainer? _container;
    private SftpStorageService? _storage;

    [DockerFact]
    public async Task PutGetListDelete_RoundTrip()
    {
        Assert.NotNull(_storage);
        await StorageRoundTrip.ExecuteAsync(_storage);
    }

    public async ValueTask InitializeAsync()
    {
        _container = new ContainerBuilder("atmoz/sftp")
            .WithCommand("testuser:testpass:1001:1001:packages")
            .WithPortBinding(22, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(22))
            .Build();

        await _container.StartAsync();

        var options = new SftpStorageOptions
        {
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(22),
            Username = "testuser",
            Password = "testpass",
            RemotePath = "/"
        };

        _storage = new SftpStorageService(new TestOptionsSnapshot<SftpStorageOptions>(options));
    }

    public async ValueTask DisposeAsync()
    {
        _storage?.Dispose();
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
