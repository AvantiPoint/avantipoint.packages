# In-Process NuGet Test Server Infrastructure

This directory contains infrastructure for testing the AvantiPoint Packages NuGet protocol implementation using an in-process test server.

## Overview

The test server infrastructure allows you to:
- Start an isolated NuGet server instance per test or test class
- Test NuGet protocol operations (push, list, exists, etc.) against a real server
- Run tests in CI without external dependencies
- Ensure tests are fast, isolated, and deterministic

## Core Components

### `NuGetTestServerHost`

The main test server harness that starts an in-process ASP.NET Core application.

**Usage:**
```csharp
await using var server = await NuGetTestServerHost.StartAsync();
var baseUrl = server.BaseAddress;
var httpClient = server.Client;
```

**Features:**
- Picks a random free TCP port (no port conflicts)
- Uses temp directory for storage (automatic cleanup)
- Configures SQLite for database (in-memory)
- Returns `BaseAddress` and `HttpClient` for making requests

### `NuGetServerFixture`

xUnit fixture for managing server lifecycle across tests in a class.

**Usage:**
```csharp
public class MyTests : IClassFixture<NuGetServerFixture>
{
    private readonly NuGetServerFixture _fixture;

    public MyTests(NuGetServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest()
    {
        var client = _fixture.Client; // NuGetClient instance
        // ... test code
    }
}
```

### `TestPackageHelper`

Utilities for creating and uploading test packages.

**Usage:**
```csharp
// Create a package
var packageBytes = TestPackageHelper.CreatePackage("MyPackage", "1.0.0");

// Upload a package
await TestPackageHelper.UploadPackageAsync(httpClient, packageBytes);

// Create and upload in one step
await TestPackageHelper.CreateAndUploadPackageAsync(
    httpClient,
    "MyPackage",
    "1.0.0");
```

## Example Test

```csharp
[Fact]
public async Task UploadPackage_ThenCheckExists_Succeeds()
{
    // Arrange
    var packageId = "Test.Package";
    var version = "1.0.0";
    
    // Act - Upload
    await TestPackageHelper.CreateAndUploadPackageAsync(
        _fixture.Server.Client,
        packageId,
        version);
    
    // Act - Check exists
    var client = _fixture.Client;
    var exists = await client.ExistsAsync(packageId, NuGetVersion.Parse(version));
    
    // Assert
    Assert.True(exists);
}
```

## Configuration

The test server is configured with:
- **API Key**: `test-api-key-12345` (required for package uploads)
- **Database**: SQLite (temp file, auto-cleanup)
- **Storage**: FileSystem (temp directory, auto-cleanup)
- **Search**: Database-based search
- **Logging**: Minimal (warnings only)

To customize configuration:
```csharp
await using var server = await NuGetTestServerHost.StartAsync(config =>
{
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ApiKey"] = "my-custom-key",
        // ... other settings
    });
});
```

## Known Limitations

Some NuGet protocol operations have limitations in the test environment:

1. **Registration Index Metadata**: The `GetPackageMetadataAsync(packageId, version)` method requires a registration index which may not be immediately available after upload.

2. **Search**: Database-based search may not work correctly or may return 400 errors in some cases.

3. **Download Stream Length**: The download API returns a stream whose `.Length` property is not supported (HTTP stream limitation).

For these operations, use the simpler alternatives that are known to work:
- Use `ExistsAsync()` instead of metadata retrieval
- Use `ListPackageVersionsAsync()` instead of search
- Read the download stream fully into a byte array instead of checking length

## CI Integration

Tests using this infrastructure run in CI automatically. They:
- ✅ Are fully in-process (no external network calls)
- ✅ Are isolated (each server instance uses unique temp directories)
- ✅ Are deterministic (no port conflicts, automatic cleanup)
- ✅ Are fast (server starts in <1 second)

## Best Practices

1. **Use xUnit Fixtures**: For tests in the same class, use `NuGetServerFixture` to share one server instance.

2. **Add Delays After Upload**: Some operations (like indexing) may not be instant. Add a small delay (100-500ms) after uploading packages.

3. **Check Package Exists First**: Before testing complex operations, verify the package exists with `ExistsAsync()`.

4. **Clean Up Temp Files**: The infrastructure automatically cleans up temp directories, but if you create additional files, ensure they're in `/tmp`.

5. **Use Unique Package IDs**: To avoid conflicts between tests, use unique package IDs per test.

## Future Improvements

Potential enhancements to the test infrastructure:

- [ ] Support for in-memory storage (eliminate file I/O)
- [ ] Better search indexing for immediate availability
- [ ] Support for symbols server testing
- [ ] Performance benchmarking helpers
- [ ] Multi-server scenarios (upstream/downstream feeds)
