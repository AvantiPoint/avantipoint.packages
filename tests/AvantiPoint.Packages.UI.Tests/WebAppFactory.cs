using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AvantiPoint.Packages.Core;
using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Tests;

// Factory for the sample OpenFeed application
internal class OpenFeedFactory : WebApplicationFactory<OpenFeed.Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // After the real services are registered, add a hosted service to seed test packages
            services.AddHostedService<TestPackageSeeder>();
        });

        var host = base.CreateHost(builder);
        return host;
    }
}

internal class TestPackageSeeder : IHostedService
{
    private readonly IServiceProvider _services;

    public TestPackageSeeder(IServiceProvider services) => _services = services;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IContext>();

        // Only seed if empty
        if (ctx.Packages.Any()) return;

        var now = DateTime.UtcNow.Date;
        var pkgs = new List<Package>
        {
            Create("Test.Alpha", "1.0.0", now.AddDays(-10), listed:true),
            Create("Test.Alpha", "1.1.0-beta", now.AddDays(-9), listed:true, prerelease:true),
            Create("Demo.Widget", "2.0.0", now.AddDays(-5), listed:true),
            Create("Demo.Widget", "2.1.0", now.AddDays(-2), listed:true),
            Create("Utility.Tools", "0.9.0", now.AddDays(-20), listed:true, prerelease:true),
        };

        ctx.Packages.AddRange(pkgs);
        await ctx.SaveChangesAsync(cancellationToken);

        static Package Create(string id, string version, DateTime published, bool listed, bool prerelease = false)
        {
            return new Package
            {
                Id = id,
                Version = NuGetVersion.Parse(version),
                Authors = new[] { "Test" },
                Description = $"Package {id} {version}",
                HasEmbeddedIcon = false,
                HasEmbeddedLicense = false,
                IsPrerelease = prerelease,
                Listed = listed,
                Published = published,
                Summary = $"Summary for {id}",
                Title = id,
                Tags = new[] { "test", "demo" },
                PackageTypes = new List<PackageType>(),
                Dependencies = new List<PackageDependency>(),
                TargetFrameworks = new List<TargetFramework>(),
                PackageDownloads = new List<PackageDownload>()
            };
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}