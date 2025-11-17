namespace AvantiPoint.Packages.Protocol.Tests.Infrastructure;

/// <summary>
/// Base fixture for tests that need an in-process NuGet server.
/// Manages server lifecycle and provides a NuGetClient for protocol tests.
/// </summary>
public class NuGetServerFixture : IAsyncLifetime
{
    private NuGetTestServerHost? _server;

    /// <summary>
    /// The running test server instance.
    /// </summary>
    public NuGetTestServerHost Server => _server ?? throw new InvalidOperationException("Server not initialized");

    /// <summary>
    /// NuGetClient configured to communicate with the test server.
    /// </summary>
    public NuGetClient Client => new NuGetClient($"{Server.BaseAddress}v3/index.json");

    /// <summary>
    /// Base URL of the test server.
    /// </summary>
    public Uri BaseUrl => Server.BaseAddress;

    public virtual async Task InitializeAsync()
    {
        _server = await NuGetTestServerHost.StartAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_server != null)
        {
            await _server.DisposeAsync();
        }
    }
}
