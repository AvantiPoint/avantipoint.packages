# DevContainer for AvantiPoint Packages

This directory contains the DevContainer configuration for developing AvantiPoint Packages in a consistent, containerized environment.

## What's Included

### Base Image
- .NET 10.0 SDK (mcr.microsoft.com/dotnet/sdk:10.0)

### Additional Tools
- **Git**: Latest version with PPA
- **Python 3.12**: For building documentation with MkDocs
- **Node.js LTS**: For any JavaScript tooling
- **Entity Framework Core CLI**: For managing database migrations
- **MkDocs & Material Theme**: For building and previewing documentation

### VS Code Extensions
- C# Dev Kit and related .NET extensions
- Docker extension
- EditorConfig support
- GitHub Pull Request integration
- Markdown linting
- Python extension

### Port Forwarding
The following ports are automatically forwarded for running sample applications:
- **5000**: OpenFeed HTTP
- **5001**: OpenFeed HTTPS
- **5002**: AuthenticatedFeed HTTP
- **5003**: AuthenticatedFeed HTTPS

## Getting Started

### Prerequisites
- [Visual Studio Code](https://code.visualstudio.com/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) for VS Code

### Opening the Project in a DevContainer

1. Open the repository folder in VS Code
2. When prompted, click **Reopen in Container**, or
3. Press `F1` and select **Dev Containers: Reopen in Container**

The first time you open the DevContainer, it will:
1. Pull the .NET 10.0 SDK image
2. Install all configured features (Git, Python, Node.js)
3. Run the post-create script to:
   - Install Python dependencies for MkDocs
   - Install Entity Framework Core tools
   - Restore NuGet packages
   - Build the solution

This process may take several minutes on the first run.

### What You Can Do

Once the DevContainer is running, you can:

#### Build and Test
```bash
# Build the solution
dotnet build APPackages.sln

# Run tests
dotnet test APPackages.sln

# Build in Release mode
dotnet build APPackages.sln -c Release
```

#### Run Sample Applications
```bash
# Run the OpenFeed sample
cd samples/OpenFeed
dotnet run

# Run the AuthenticatedFeed sample
cd samples/AuthenticatedFeed
dotnet run
```

#### Work with Entity Framework Migrations
```bash
# Add a new migration (SQL Server example)
dotnet ef migrations add MigrationName --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer

# Update database
dotnet ef database update --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer
```

#### Preview Documentation
```bash
# Build and serve documentation locally
mkdocs serve

# Then open http://localhost:8000 in your browser
```

## Environment Variables

The DevContainer sets the following environment variables:
- `DOTNET_CLI_TELEMETRY_OPTOUT=1`: Disables .NET CLI telemetry
- `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`: Skips first-time experience
- `ASPNETCORE_ENVIRONMENT=Development`: Sets ASP.NET Core environment to Development

## Git Configuration

Your local Git configuration (`~/.gitconfig`) is automatically mounted into the container, so your Git identity and settings are preserved.

## Customization

You can customize the DevContainer by editing:
- `.devcontainer/devcontainer.json`: Main configuration file
- `.devcontainer/post-create.sh`: Post-creation setup script

After making changes to the configuration, rebuild the container:
1. Press `F1`
2. Select **Dev Containers: Rebuild Container**

## Troubleshooting

### Container fails to build
- Ensure Docker Desktop is running
- Check that you have sufficient disk space
- Try rebuilding the container: **Dev Containers: Rebuild Container**

### Missing .NET tools
If `dotnet ef` or other tools aren't found, add them to your PATH:
```bash
export PATH="$PATH:/root/.dotnet/tools"
```

Or re-run the post-create script:
```bash
bash .devcontainer/post-create.sh
```

### Port already in use
If a port is already in use on your host machine, you can change the forwarded ports in `devcontainer.json`.

## More Information

- [VS Code Dev Containers Documentation](https://code.visualstudio.com/docs/devcontainers/containers)
- [AvantiPoint Packages Documentation](https://avantipoint.github.io/avantipoint.packages/)
