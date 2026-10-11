using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Host.Pages.Native;
using AvantiPoint.Packages.Registry.Native;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class ManagedHostFactory(bool enabled = true, bool anonymous = true) : WebApplicationFactory<NativePageModel>
{
    private readonly string _root = Directory.CreateTempSubdirectory("avp-host-ui-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Type"] = "Sqlite",
            ["Database:ConnectionString"] = $"Data Source={_root}/packages.db",
            ["Storage:Type"] = "FileSystem",
            ["Storage:Path"] = _root + "/artifacts",
            ["Feed:PublicBaseUrl"] = "https://registry.test/prefix/",
            ["Feed:Authentication:AllowAnonymousPull"] = anonymous.ToString(),
            ["Logging:LogLevel:Default"] = "Warning",
        }));
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (!enabled) return;
            var registry = (IFeedRegistry)services.Single(descriptor => descriptor.ServiceType == typeof(IFeedRegistry)).ImplementationInstance!;
            foreach (var protocol in new[] { FeedProtocol.Maven, FeedProtocol.Swift, FeedProtocol.Pub })
            {
                var name = protocol.ToString().ToLowerInvariant();
                registry.Register(new(name, protocol, null, "/" + name, "Feed:" + protocol));
                services.AddOptions<NativeRegistryOptions>(protocol.ToString()).BindConfiguration("Feed:" + protocol);
            }
            services.AddScoped<NativeArtifactStore>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Directory.Delete(_root, recursive: true);
    }
}
