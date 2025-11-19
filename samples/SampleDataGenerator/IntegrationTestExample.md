# Integration Test Example

This document demonstrates how to disable the sample data seeder in integration tests.

## Why Disable the Seeder?

In integration tests, you typically want:
- **Faster test execution**: Seeding from NuGet.org takes time and requires network access
- **Deterministic data**: You want to control exactly what data exists for each test
- **Isolated tests**: Each test should start with a clean, predictable state
- **No external dependencies**: Tests shouldn't rely on NuGet.org being available

## Example Integration Test

Here's an example of how to disable the seeder when creating a test application:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleDataGenerator;
using Xunit;

public class MyIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MyIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Override the sample data seeder to disable it
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the default seeder registration
                var seederDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IHostedService) &&
                         d.ImplementationType == typeof(PackageSeederHostedService));
                
                if (seederDescriptor != null)
                {
                    services.Remove(seederDescriptor);
                }

                // Re-register with seeding disabled
                services.AddSampleDataSeeder(options =>
                {
                    options.Enabled = false;
                });
            });
        });
    }

    [Fact]
    public async Task Can_Query_Empty_Feed()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/v3/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
```

## Alternative Approach: Configuration-Based

You can also control the seeder via configuration in your test setup:

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test-specific configuration
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["SampleDataSeeder:Enabled"] = "false"
            });
        });
        
        builder.ConfigureServices(services =>
        {
            // Use configuration to control seeder
            services.AddSampleDataSeeder(options =>
            {
                var config = services.BuildServiceProvider()
                    .GetRequiredService<IConfiguration>();
                options.Enabled = config.GetValue<bool>("SampleDataSeeder:Enabled", true);
            });
        });
    }
}
```

## Programmatic Control in Application Code

For sample applications that might be used in tests, you can add this to `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// ... other service registrations

// Only enable seeder in non-test environments
var enableSeeder = !builder.Environment.IsEnvironment("Testing");
builder.Services.AddSampleDataSeeder(options =>
{
    options.Enabled = enableSeeder;
});
```

Then in your test:

```csharp
_factory = factory.WithWebHostBuilder(builder =>
{
    builder.UseEnvironment("Testing");
});
```

## Best Practices

1. **Always disable seeder in integration tests** - Use test-specific data instead
2. **Seed test data programmatically** - Create only the data needed for each test
3. **Use in-memory database for tests** - Faster and doesn't require cleanup
4. **Verify seeder is disabled** - Add a test that confirms no seeding happens

Example verification test:

```csharp
[Fact]
public void Seeder_Is_Disabled_In_Tests()
{
    // Arrange
    var services = _factory.Services;
    
    // Act
    var seederOptions = services.GetRequiredService<SampleDataSeederOptions>();
    
    // Assert
    Assert.False(seederOptions.Enabled, "Sample data seeder should be disabled in tests");
}
```
