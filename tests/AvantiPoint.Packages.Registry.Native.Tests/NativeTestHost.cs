using System.Net.Http.Headers;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Registry.Native.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AvantiPoint.Packages.Registry.Native.Tests;

internal sealed class NativeTestHost : IAsyncDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "native-feed-test-" + Guid.NewGuid().ToString("N"));
    private WebApplication _app = null!;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _app.Services;

    public static async Task<NativeTestHost> StartAsync(bool anonymous = false, bool realHttp = false, bool pathAuthorization = false, bool customChallenge = false)
    {
        var host = new NativeTestHost();
        Directory.CreateDirectory(host._root);
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(options => options.ValidateScopes = true);
        if (realHttp) builder.WebHost.UseKestrel(o => o.Listen(System.Net.IPAddress.Loopback, 0));
        else builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Feed:Name"] = "native-tests",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Feed:PublicBaseUrl"] = realHttp ? "http://127.0.0.1" : "https://registry.test",
            ["Feed:Authentication:AllowAnonymousPull"] = anonymous.ToString(),
            ["Feed:Maven:MaxArtifactBytes"] = "1048576",
            ["Feed:Swift:MaxArtifactBytes"] = "1048576",
            ["Feed:Pub:MaxArtifactBytes"] = "1048576",
        });
        builder.Services.AddDbContext<SqliteContext>(o => o.UseSqlite($"Data Source={host._root}/feed.db"));
        builder.Services.AddScoped<IContext>(sp => sp.GetRequiredService<SqliteContext>());
        builder.Services.Configure<FileSystemStorageOptions>(o => o.Path = host._root + "/artifacts");
        builder.Services.AddScoped<IStorageService, FileStorageService>();
        builder.Services.AddSingleton<IFeedTokenAuthenticationService>(new TestTokenAuthentication(customChallenge));
        builder.Services.AddSingleton<IFeedActionHandler>(new TestArtifactHandler(pathAuthorization));
        var feed = builder.AddAvantiPointFeed(builder.Configuration.GetSection("Feed"));
        feed.UseNativeRegistry(FeedProtocol.Maven).UseNativeRegistry(FeedProtocol.Swift).UseNativeRegistry(FeedProtocol.Pub);
        host._app = builder.Build();
        host._app.UseAvantiPointFeedPlatform();
        host._app.UseRouting();
        host._app.MapNativeFeeds(feed);
        using (var scope = host._app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SqliteContext>().Database.EnsureCreatedAsync();
        await host._app.StartAsync();
        if (realHttp)
        {
            var server = host.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var address = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single();
            host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AvantiPoint.Feed.Platform.Configuration.FeedOptions>>()
                .CurrentValue.PublicBaseUrl = address;
            host.Client = new HttpClient { BaseAddress = new Uri(address) };
        }
        else
        {
            host.Client = host._app.GetTestClient();
            host.Client.BaseAddress = new Uri("https://registry.test");
        }
        return host;
    }

    public void Authenticate(string token) => Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
        Directory.Delete(_root, true);
    }

    private sealed class TestTokenAuthentication(bool customChallenge) : IFeedTokenAuthenticationService
    {
        public Task<FeedAuthenticationResult> AuthenticateTokenAsync(string token, FeedOperation operation, string? username, CancellationToken cancellationToken = default)
        {
            if (customChallenge) return Task.FromResult(FeedAuthenticationResult.Fail("Rotate token.", new Dictionary<string, string>
            {
                ["WWW-Authenticate"] = "Bearer realm=\"native-test\", error=\"invalid_token\"",
                ["X-Feed-Recovery"] = "rotate-token",
            }));
            if (token is not ("reader" or "writer")) return Task.FromResult(FeedAuthenticationResult.Fail("Invalid."));
            if (username is not null && username != "person@example.test") return Task.FromResult(FeedAuthenticationResult.Fail("Invalid."));
            return Task.FromResult(operation == FeedOperation.Push && token != "writer"
                ? FeedAuthenticationResult.Forbidden("Read only.") : FeedAuthenticationResult.Success());
        }
    }

    private sealed class TestArtifactHandler(bool pathAuthorization) : IFeedActionHandler
    {
        public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(!context.ArtifactName.Contains("denied", StringComparison.Ordinal)
                && (!pathAuthorization || context.DigestOrTarballPath?.EndsWith(".aar", StringComparison.Ordinal) == true));
        public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
