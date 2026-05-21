using System.IO;
using System.Linq;
using AvantiPoint.Packages.Core;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGet.Versioning;
using SampleDataGenerator;
namespace AvantiPoint.Packages.UI.Tests;

internal class TestPackageSeeder(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IContext>();

        // Wait a bit for database to be ready
        await Task.Delay(100, cancellationToken);

        // Only seed if empty - check for existing packages
        var existingPackages = await ctx.Packages
            .Where(p => p.Id == "Test.Alpha" || p.Id == "Demo.Widget" || p.Id == "Utility.Tools")
            .AnyAsync(cancellationToken);
        
        if (existingPackages)
        {
            return;
        }

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
        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Ignore if packages already exist (race condition or duplicate insert)
        }

        static Package Create(string id, string version, DateTime published, bool listed, bool prerelease = false)
        {
            return new Package
            {
                Id = id,
                Version = NuGetVersion.Parse(version),
                Authors = ["Test"],
                Description = $"Package {id} {version}",
                HasEmbeddedIcon = false,
                HasEmbeddedLicense = false,
                IsPrerelease = prerelease,
                Listed = listed,
                Published = published,
                Summary = $"Summary for {id}",
                Title = id,
                Tags = ["test", "demo"],
                PackageTypes = [],
                Dependencies = [],
                TargetFrameworks = [],
                PackageDownloads = []
            };
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
