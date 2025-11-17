# Sample Data Generator

This library provides automatic seeding of NuGet feeds with sample packages from NuGet.org.

## Features

- **Automatic Database Detection**: Only seeds if the database is empty
- **Hosted Service**: Runs automatically on application startup
- **Configurable Package List**: Easy to customize which packages to download
- **Version Control**: Specify how many versions of each package to download
- **Prerelease Support**: Choose whether to include prerelease versions
- **Comprehensive Logging**: Detailed logs of the seeding process

## Usage

### 1. Add Project Reference

Add a reference to the SampleDataGenerator project in your feed project:

```xml
<ItemGroup>
  <ProjectReference Include="..\SampleDataGenerator\SampleDataGenerator.csproj" />
</ItemGroup>
```

### 2. Register the Service

In your `Program.cs`, add the sample data seeder:

```csharp
using SampleDataGenerator;

var builder = WebApplication.CreateBuilder(args);

// ... other service registrations

// Add sample data seeder (enabled by default)
builder.Services.AddSampleDataSeeder();

var app = builder.Build();
```

**Disabling for Integration Tests:**

You can disable the seeder by passing configuration options:

```csharp
// Disable seeding for integration tests
builder.Services.AddSampleDataSeeder(options =>
{
    options.Enabled = false;
});
```

This is useful for integration tests where you want to control the data manually or use in-memory databases with specific test data.

### 3. Run Your Application

On first run, when the database is empty, the seeder will:
1. Check if the database has any packages
2. If empty, download packages from NuGet.org according to the predefined list
3. Index each package into your local feed
4. Log progress and results

## Customizing Packages

To customize which packages are downloaded, edit `SamplePackages.cs`:

```csharp
public static IReadOnlyList<PackageDefinition> Packages { get; } =
[
    new() { PackageId = "YourPackage", MaxVersions = 3, IncludePrerelease = true },
    // Add more packages...
];
```

## Package Definition Options

- **PackageId**: The NuGet package ID (required)
- **MaxVersions**: Maximum number of versions to download (default: 3)
- **IncludePrerelease**: Whether to include prerelease versions (default: true)

## Current Sample Packages

The default configuration includes approximately 20 packages:

- **Dan Siegel's Packages**: Mobile.BuildTools, Prism libraries, AP packages
- **Microsoft Packages**: Core libraries, ASP.NET Core, Entity Framework
- **Popular Community Packages**: Newtonsoft.Json, Serilog, DryIoc, Polly

This provides a good mix of stable and prerelease packages with multiple versions for testing.

## Logging

The seeder provides detailed logging:
- Database check status
- Package download progress
- Indexing results
- Error details
- Final summary

## Performance

The seeder includes:
- Small delays between downloads to avoid overwhelming NuGet.org
- Efficient memory stream handling
- Graceful error handling per package
- Continues on individual package failures

## Notes

- Only runs on application startup
- Only seeds if database is completely empty
- Downloads packages in version order (latest first)
- Automatically skips packages that already exist
- Uses the official NuGet.org v3 API
