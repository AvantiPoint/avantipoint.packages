# Samples

This folder contains sample applications demonstrating how to set up and configure AvantiPoint Packages.

## OpenFeed

A simple, open NuGet feed without authentication. This is the minimal setup needed to run a package feed.

**Features:**
- No authentication required
- SQLite or SQL Server database (configurable)
- Local file storage
- Optional upstream mirror to NuGet.org
- Blazor UI with package search functionality
- Automatic sample data seeding from NuGet.org

**Use case:** Development, testing, or internal teams where authentication isn't needed.

**Location:** [OpenFeed](./OpenFeed)

## AuthenticatedFeed

A secured NuGet feed with authentication and event callbacks.

**Features:**
- Custom authentication service (`IPackageAuthenticationService`)
- Event callbacks (`INuGetFeedActionHandler`) with examples for:
  - Access control (who can download which packages)
  - Download/upload metrics collection
  - Email notifications
- SQL Server database
- Local file storage
- Automatic sample data seeding from NuGet.org

**Use case:** Production feeds where you need to control access and track usage.

**Location:** [AuthenticatedFeed](./AuthenticatedFeed)

## SampleDataGenerator

A shared library that automatically seeds feeds with real packages from NuGet.org on first run.

**Features:**
- Hosted service that runs on application startup
- Only seeds if database is empty
- Downloads ~20 packages with multiple versions each
- Configurable package list (Dan Siegel's packages, Microsoft libraries, popular community packages)
- Comprehensive logging of download and indexing progress

**Use case:** Quickly populate a new feed with real data for testing and demonstration purposes.

**Location:** [SampleDataGenerator](./SampleDataGenerator)

See [SampleDataGenerator/README.md](SampleDataGenerator/README.md) for detailed documentation.

## Running the Samples

### Prerequisites

- .NET 10.0 SDK or later
- (Optional) SQL Server for production scenarios

### OpenFeed

1. Navigate to the OpenFeed directory:
   ```bash
   cd samples/OpenFeed
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. On first run, the sample data seeder will automatically download and index packages from NuGet.org

4. The feed will be available at `https://localhost:5001/v3/index.json`

5. Browse the package search UI at `https://localhost:5001`

6. Add it as a NuGet source:
   ```bash
   dotnet nuget add source https://localhost:5001/v3/index.json --name LocalFeed
   ```

### AuthenticatedFeed

1. Navigate to the AuthenticatedFeed directory:
   ```bash
   cd samples/AuthenticatedFeed
   ```

2. Update the connection string in `appsettings.json` if needed

3. Run the application:
   ```bash
   dotnet run
   ```

4. On first run, the sample data seeder will automatically download and index packages from NuGet.org

5. The feed will be available at `https://localhost:5001/v3/index.json`

6. Add it with credentials (see the demo authentication service for valid credentials):
   ```bash
   dotnet nuget add source https://localhost:5001/v3/index.json --name AuthFeed
   dotnet nuget update source AuthFeed --username demo@example.com --password demo-token
   ```

## Sample Data Seeding

Both OpenFeed and AuthenticatedFeed now include automatic sample data seeding powered by the SampleDataGenerator library.

**How it works:**
1. On application startup, the seeder checks if the database has any packages
2. If empty, it downloads packages from NuGet.org according to the predefined list
3. Each package is downloaded and indexed into your local feed
4. Progress is logged with detailed information

**Default packages include:**
- Dan Siegel's packages: Mobile.BuildTools, Prism libraries, AP packages
- Microsoft packages: DI, Logging, Configuration, ASP.NET Core, Entity Framework
- Popular community packages: Newtonsoft.Json, Serilog, DryIoc, Polly

This gives you a feed with real, multi-version packages to test search, versioning, and download functionality.

## Learning from the Samples

### Key Files to Review

**OpenFeed:**
- `Program.cs` - Minimal setup with service registration
- `appsettings.json` - Basic configuration

**AuthenticatedFeed:**
- `Program.cs` - Full setup with authentication and callbacks
- `Services/DemoNuGetAuthenticationService.cs` - Example authentication implementation
- `Services/DemoActionHandler.cs` - Example event handler implementation
- `appsettings.json` - Production-ready configuration

### Next Steps

After reviewing the samples:

1. Read the [Getting Started Guide](../docs/getting-started.md)
2. Review the [Authentication Documentation](../docs/authentication.md)
3. Review the [Callbacks Documentation](../docs/callbacks.md)
4. Explore [Hosting Options](../docs/hosting.md)

## Customizing for Your Needs

These samples provide a starting point. You'll want to customize:

1. **Authentication** - Connect to your user database or identity provider
2. **Authorization** - Implement your specific access control rules
3. **Storage** - Use cloud storage (Azure Blob or AWS S3) for production
4. **Database** - Use SQL Server, MySQL, or PostgreSQL for production
5. **Callbacks** - Implement your specific business logic (emails, metrics, etc.)

See the [full documentation](../docs/index.md) for detailed guides on each topic.
