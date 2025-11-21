# Integration Test API

A minimal API-only project for integration testing of the AvantiPoint.Packages NuGet feed. This project is designed to be used with `WebApplicationFactory` for comprehensive integration testing.

## Features

- ✅ **Minimal API-only** - No UI, no OpenAPI docs, no sample data seeding
- ✅ **Dynamic provider selection** - Database and storage providers configured via `appsettings.json`
- ✅ **All database providers** - Supports Sqlite, SqlServer (MySql can be added)
- ✅ **All storage providers** - Supports FileSystem, AzureBlobStorage, AwsS3
- ✅ **Repository signing support** - Configurable via `appsettings.json`
- ✅ **Test-friendly** - `Program` class is public for `WebApplicationFactory` usage

## Configuration

**Note:** The `appsettings.json` file is intentionally minimal. Integration tests should configure all settings via `ConfigureAppConfiguration` in `WebApplicationFactory`. This ensures tests have full control over the application configuration.

### Database Provider

Configure `Database:Type` in your test setup:

```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    { "Database:Type", "Sqlite" },  // Options: "Sqlite", "SqlServer", "MySql"
    { "ConnectionStrings:Sqlite", "DataSource=:memory:" }
});
```

### Storage Provider

Configure `Storage:Type` in your test setup:

```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    { "Storage:Type", "FileSystem" },  // Options: "FileSystem", "AzureBlobStorage", "AwsS3"
    { "Storage:Path", "App_Data/packages" }
});
```

For Azure Blob Storage:
```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    { "Storage:Type", "AzureBlobStorage" },
    { "Storage:ConnectionString", "DefaultEndpointsProtocol=https;AccountName=..." },
    { "Storage:Container", "packages" }
});
```

For AWS S3:
```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    { "Storage:Type", "AwsS3" },
    { "Storage:Bucket", "packages" },
    { "Storage:Region", "us-east-1" },
    { "Storage:AccessKey", "..." },
    { "Storage:SecretKey", "..." }
});
```

### Repository Signing

Configure signing in your test setup:

```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    { "Signing:Mode", "SelfSigned" },  // Options: null (disabled), "SelfSigned", "StoredCertificate"
    { "Signing:SelfSigned:SubjectName", "CN=Test Repository Signer" },
    { "Signing:SelfSigned:KeySize", "KeySize4096" }
});
```

## Usage in Tests

**Note:** To use IntegrationTestApi in tests, you need to:
1. Add a project reference to `IntegrationTestApi.csproj` in your test project
2. Use `IntegrationTestApi.Program` explicitly (or add a global using alias)

### Basic Example

```csharp
using IntegrationTestApi;

public class MyIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public MyIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Override configuration for tests
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Database:Type", "Sqlite" },
                        { "ConnectionStrings:Sqlite", "DataSource=:memory:" },
                        { "Storage:Type", "FileSystem" },
                        { "Storage:Path", "App_Data/packages" }
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Replace services for testing if needed
                });
            });
    }

    [Fact]
    public async Task TestSomething()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v3/index.json");
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
```

### Testing Different Providers

You can create test fixtures for different provider combinations:

```csharp
public class SqlServerFileSystemTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SqlServerFileSystemTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Database:Type", "SqlServer" },
                    { "ConnectionStrings:SqlServer", "Server=(localdb)\\mssqllocaldb;Database=TestPackages;..." },
                    { "Storage:Type", "FileSystem" }
                });
            });
        });
    }
}
```

## Project Structure

```
samples/IntegrationTestApi/
├── Program.cs              # Minimal API setup with dynamic provider selection
├── IntegrationTestApi.csproj
├── appsettings.json        # Default configuration
└── appsettings.Development.json
```

## Differences from OpenFeed

| Feature | OpenFeed | IntegrationTestApi |
|---------|----------|-------------------|
| UI (Blazor) | ✅ | ❌ |
| OpenAPI Docs | ✅ | ❌ |
| Sample Data Seeding | ✅ | ❌ |
| Static Files | ✅ | ❌ |
| API Routes | ✅ | ✅ |
| Dynamic Providers | ⚠️ Partial | ✅ Full |
| Test-Friendly | ⚠️ | ✅ |

## Adding MySql Support

To enable MySql support:

1. Uncomment the MySql project reference in `IntegrationTestApi.csproj`:
   ```xml
   <ProjectReference Include="..\..\src\AvantiPoint.Packages.Database.MySql\AvantiPoint.Packages.Database.MySql.csproj" />
   ```

2. Update `Program.cs` to use MySql:
   ```csharp
   case "MySql":
       options.AddMySqlDatabase("MySql");
       break;
   ```

Note: MySql may have version conflicts with EF Core. Check compatibility before enabling.

