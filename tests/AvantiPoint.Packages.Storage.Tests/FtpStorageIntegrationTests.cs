using AvantiPoint.Packages.Ftp;
using AvantiPoint.Packages.Ftp.Storage;
using AvantiPoint.Packages.Storage.Tests.TestInfrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests;

[Collection("StorageIntegration")]
public sealed class FtpStorageIntegrationTests : IAsyncLifetime
{
    private const int PasvMinPort = 21100;
    private const int PasvMaxPort = 21105;

    private IContainer? _container;
    private FtpStorageService? _storage;

    [DockerFact]
    public async Task PutGetListDelete_RoundTrip()
    {
        Assert.NotNull(_storage);
        await StorageRoundTrip.ExecuteAsync(_storage);
    }

    public async ValueTask InitializeAsync()
    {
        var containerBuilder = new ContainerBuilder("fauria/vsftpd")
            .WithEnvironment("FTP_USER", "testuser")
            .WithEnvironment("FTP_PASS", "testpass")
            .WithEnvironment("PASV_ADDRESS", "127.0.0.1")
            .WithEnvironment("PASV_MIN_PORT", PasvMinPort.ToString())
            .WithEnvironment("PASV_MAX_PORT", PasvMaxPort.ToString())
            .WithPortBinding(21, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(21));

        for (var port = PasvMinPort; port <= PasvMaxPort; port++)
        {
            containerBuilder = containerBuilder.WithPortBinding(port, port);
        }

        _container = containerBuilder.Build();

        await _container.StartAsync();

        var options = new FtpStorageOptions
        {
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(21),
            Username = "testuser",
            Password = "testpass",
            RemotePath = "/",
            UsePassiveMode = true,
            PassiveAddress = "127.0.0.1"
        };

        _storage = new FtpStorageService(new TestOptionsSnapshot<FtpStorageOptions>(options));
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
