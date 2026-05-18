using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Gcp;
using AvantiPoint.Packages.Gcp.Storage;
using AvantiPoint.Packages.Storage.Tests.TestInfrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests;

[Collection("StorageIntegration")]
public sealed class GcsStorageIntegrationTests : IAsyncLifetime
{
    private const string BucketName = "packages-test";
    private IContainer? _container;
    private GcsStorageService? _storage;

    [DockerFact]
    public async Task PutGetListDelete_RoundTrip()
    {
        Assert.NotNull(_storage);
        await StorageRoundTrip.ExecuteAsync(_storage);
    }

    public async ValueTask InitializeAsync()
    {
        _container = new ContainerBuilder("fsouza/fake-gcs-server")
            .WithCommand("-scheme", "http")
            .WithPortBinding(4443, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(4443))
            .Build();

        await _container.StartAsync();

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(4443);
        var emulatorHost = $"http://{host}:{port}";

        using var http = new HttpClient { BaseAddress = new Uri(emulatorHost) };
        var emulatorAuthority = new Uri(emulatorHost).Authority;
        var configJson = JsonSerializer.Serialize(new
        {
            externalUrl = emulatorHost,
            publicHost = emulatorAuthority
        });
        using var configContent = new StringContent(configJson, Encoding.UTF8, "application/json");
        var configResponse = await http.PutAsync("/_internal/config", configContent);
        configResponse.EnsureSuccessStatusCode();
        var response = await http.PostAsJsonAsync(
            "/storage/v1/b?project=test",
            new { name = BucketName });

        response.EnsureSuccessStatusCode();

        var options = new GcsStorageOptions
        {
            Bucket = BucketName,
            Prefix = string.Empty,
            EmulatorHost = emulatorHost,
            UseEmulator = true
        };

        var client = GcsStorageClientFactory.Create(Options.Create(options));
        _storage = new GcsStorageService(new TestOptionsSnapshot<GcsStorageOptions>(options), client);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
