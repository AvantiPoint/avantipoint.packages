# AvantiPoint Packages - GitHub Copilot Instructions

## Project Overview

AvantiPoint Packages is a modern .NET NuGet feed solution based on BaGet, providing custom authenticated package feeds with advanced user authentication and callback hooks. The project targets .NET 10.0 and includes support for multiple storage backends (Azure, AWS, SQL Server, SQLite, MySQL).

### Key Components

- **AvantiPoint.Packages.Core**: Core functionality and abstractions
- **AvantiPoint.Packages.Hosting**: ASP.NET Core hosting integration
- **AvantiPoint.Packages.Protocol**: NuGet protocol implementation
- **AvantiPoint.Packages.Azure**: Azure Blob Storage integration
- **AvantiPoint.Packages.Aws**: AWS S3 Storage integration
- **AvantiPoint.Packages.Database.***: Database providers (SQL Server, SQLite, MySQL)

### Sample Applications

- **OpenFeed**: Simple, open NuGet feed without authentication
- **AuthenticatedFeed**: Secured feed with authentication and callbacks

## Build and Test

### Prerequisites

- .NET 10.0 SDK (see `global.json` for exact version requirements)
- Solution uses Central Package Management (see `Directory.Packages.props`)

### Build Commands

```bash
# Restore dependencies
dotnet restore APPackages.sln

# Build the solution
dotnet build APPackages.sln

# Build in Release mode
dotnet build APPackages.sln -c Release
```

### Running Samples

```bash
# Run the OpenFeed sample
dotnet run --project samples/OpenFeed/OpenFeed.csproj

# Run the AuthenticatedFeed sample
dotnet run --project samples/AuthenticatedFeed/AuthenticatedFeed.csproj
```

### Testing

This repository currently does not have a dedicated test project. When making changes, verify by:
1. Building the solution successfully
2. Running the sample applications
3. Testing NuGet operations (push, restore, install) against the sample feeds

## Coding Standards and Conventions

### General Guidelines

- Use C# latest language features (as specified in `Directory.Build.props`)
- Follow standard .NET naming conventions
- Keep code consistent with the existing codebase style
- All public APIs should have XML documentation comments
- Use async/await for I/O operations

### File Organization

- Source code in `src/` directory
- Sample applications in `samples/` directory
- Documentation in `docs/` directory
- Build configuration files at solution root

### Project Configuration

- Use Central Package Management - add package references to `Directory.Packages.props`
- All projects inherit from `Directory.Build.props` and `Directory.Build.targets`
- Version management handled by Nerdbank.GitVersioning (see `version.json`)

## Authentication and Security

### Important Security Considerations

- **Never commit secrets or API keys** to the repository
- Authentication is handled through `IPackageAuthenticationService` interface
- Two authentication methods supported:
  1. **API Key authentication** (for package publishing)
  2. **Basic authentication** (username + token for package consumers)
- Authentication is separate from ASP.NET Core authentication

### Key Interfaces

- `IPackageAuthenticationService`: Implement for custom user authentication
- `INuGetFeedActionHandler`: Implement for handling upload/download events and callbacks

## Dependencies and Frameworks

### Core Dependencies

- ASP.NET Core (for web hosting)
- Entity Framework Core (for database operations)
- NuGet.Protocol (for NuGet operations)
- Azure Storage / AWS SDK (for cloud storage)

### Package Management

- All package versions are centrally managed in `Directory.Packages.props`
- Use `<PackageReference Include="PackageName" />` without Version attribute in project files
- Add version specifications only to `Directory.Packages.props`

## Common Tasks

### Adding a New Package Dependency

1. Add package reference to `Directory.Packages.props` with version
2. Add `<PackageReference Include="PackageName" />` to relevant `.csproj` files
3. Build and test to ensure compatibility

### Adding a New Storage Provider

1. Create new project in `src/` directory following naming pattern: `AvantiPoint.Packages.<Provider>`
2. Implement required storage interfaces from `AvantiPoint.Packages.Core`
3. Add dependency injection extensions for service registration
4. Update solution file and documentation

### Modifying Authentication Logic

1. Changes to authentication should be made in `AvantiPoint.Packages.Core`
2. Update both sample projects to demonstrate new authentication features
3. Document breaking changes in commit messages

## Documentation

- Main documentation is in the `docs/` directory (MkDocs format)
- Documentation site configuration in `mkdocs.yml`
- Update documentation when adding new features or changing public APIs
- README.md should contain getting started information

## CI/CD

- Build workflow: `.github/workflows/build-packages.yml`
- Documentation workflow: `.github/workflows/docs.yml`
- Uses reusable workflows from `avantipoint/workflow-templates`
- Builds triggered on push to `master` and pull requests
- NuGet packages deployed to internal feed on successful builds

## Tips for Working with This Repository

- When modifying core functionality, test against both sample applications
- Check that changes don't break existing authentication implementations
- Ensure database migrations are compatible with all supported database providers
- Consider backward compatibility when changing public APIs
- Update documentation site for user-facing changes
