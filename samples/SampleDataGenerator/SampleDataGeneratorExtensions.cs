using System;
using Microsoft.Extensions.DependencyInjection;

namespace SampleDataGenerator;

/// <summary>
/// Configuration options for the sample data seeder
/// </summary>
public class SampleDataSeederOptions
{
    /// <summary>
    /// Gets or sets whether the seeder is enabled. Default is true.
    /// Set to false in integration tests to disable automatic seeding.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Extension methods for registering the sample data generator services
/// </summary>
public static class SampleDataGeneratorExtensions
{
    /// <summary>
    /// Adds the sample data seeder hosted service to the service collection.
    /// This will automatically seed the feed with packages from NuGet.org if the database is empty.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSampleDataSeeder(this IServiceCollection services)
    {
        return AddSampleDataSeeder(services, options => { });
    }

    /// <summary>
    /// Adds the sample data seeder hosted service to the service collection with configuration.
    /// This will automatically seed the feed with packages from NuGet.org if the database is empty.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for seeder options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSampleDataSeeder(this IServiceCollection services, Action<SampleDataSeederOptions> configure)
    {
        var options = new SampleDataSeederOptions();
        configure(options);
        services.AddSingleton(options);
        services.AddHostedService<PackageSeederHostedService>();
        return services;
    }
}
