using AvantiPoint.Packages.Sftp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

    [DockerFact]
    public async Task StreamingDownloadWorksWithSynchronousHttpIoDisabled()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        Assert.NotNull(_storage);
        const string path = "packages/native/http-stream/data";
        var payload = new byte[32768];
        Random.Shared.NextBytes(payload);
        await using var upload = new MemoryStream(payload);
        await _storage.UploadAsync(path, upload, "application/octet-stream", ct);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.AllowSynchronousIO = false);
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapGet("/artifact", (HttpContext http) => Results.Stream(
                destination => _storage.CopyToAsync(path, destination, http.RequestAborted)));
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        Assert.Equal(payload, await client.GetByteArrayAsync("/artifact", ct));
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
