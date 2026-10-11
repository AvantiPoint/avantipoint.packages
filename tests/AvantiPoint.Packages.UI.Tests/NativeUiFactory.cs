using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Registry.Native;
using AvantiPoint.Packages.Registry.Native.Storage;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeUiFactory(bool enabled = true, bool anonymous = true) : OpenFeedFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var sampleSeeder = services.SingleOrDefault(descriptor => descriptor.ImplementationType == typeof(SampleDataGenerator.PackageSeederHostedService));
            if (sampleSeeder is not null) services.Remove(sampleSeeder);
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
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Feed:PublicBaseUrl"] = "https://registry.test/prefix/",
            ["Feed:Authentication:AllowAnonymousPull"] = anonymous.ToString(),
            ["Feed:Maven:Enabled"] = enabled.ToString(),
            ["Feed:Swift:Enabled"] = enabled.ToString(),
            ["Feed:Pub:Enabled"] = enabled.ToString(),
        }));
    }
}
