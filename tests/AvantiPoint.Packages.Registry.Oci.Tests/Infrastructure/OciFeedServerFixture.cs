using System.Net.Http.Headers;
using AvantiPoint.Packages.Registry.Tests.Shared;

namespace AvantiPoint.Packages.Registry.Oci.Tests.Infrastructure;

public sealed class OciFeedServerFixture : IAsyncLifetime
{
    private FeedTestServerHost? _server;

    public FeedTestServerHost Server => _server ?? throw new InvalidOperationException("Server not initialized.");

    public HttpClient AuthenticatedClient { get; private set; } = null!;

    public string DockerRegistryHost => Server.DockerRegistryHost;

    public async ValueTask InitializeAsync()
    {
        _server = await FeedTestServerHost.StartAsync();
        AuthenticatedClient = _server.Client;
        AuthenticatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            FeedTestServerHost.DefaultApiKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }
}
