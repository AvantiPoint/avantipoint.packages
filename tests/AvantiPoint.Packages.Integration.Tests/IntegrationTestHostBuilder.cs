using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Integration.Tests;

internal static class IntegrationTestHostBuilder
{
    public const string ServiceIndexUrl = "https://example.test/v3/index.json";

    public static void ConfigureDefaultTestApp(
        IWebHostBuilder builder,
        string storagePath,
        Action<Dictionary<string, string?>>? configure = null)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                { "Database:Type", "Sqlite" },
                { "ConnectionStrings:Sqlite", "DataSource=:memory:" },
                { "Storage:Type", "FileSystem" },
                { "Storage:Path", storagePath }
            };

            configure?.Invoke(settings);
            config.AddInMemoryCollection(settings);
        });
    }

    public static void UseTestUrlGenerator(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUrlGenerator>();
            services.AddSingleton<IUrlGenerator, IntegrationTestUrlGenerator>();
        });
    }
}

