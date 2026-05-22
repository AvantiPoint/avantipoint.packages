using AvantiPoint.Packages.Registry.Tests.Shared;

namespace AvantiPoint.Packages.Registry.Npm.Tests.Infrastructure;

public sealed class NpmFeedServerFixture : IAsyncLifetime
{
    private FeedTestServerHost? _server;

    public FeedTestServerHost Server => _server ?? throw new InvalidOperationException("Server not initialized.");

    public Uri NpmRegistryUrl => Server.NpmRegistryUrl;

    public async ValueTask InitializeAsync()
    {
        _server = await FeedTestServerHost.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }
}
