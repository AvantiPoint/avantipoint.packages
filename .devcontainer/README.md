# DevContainer for AvantiPoint Packages

This directory contains the DevContainer configuration for developing AvantiPoint Packages in a consistent, containerized environment.

## What's Included

### Base Image
- .NET 10.0 SDK (mcr.microsoft.com/dotnet/sdk:10.0)

### Database Services
- **SQL Server 2022** (Developer Edition) on port 1433
- **MySQL 8.0** on port 3306
- **PostgreSQL 16** on port 5432
- **DbGate** database management UI on port 3000

All databases come pre-configured with:
- Default database: `AvantiPointPackages`
- Development credentials (see connection strings below)
- Data persistence via Docker volumes
- Health checks to ensure availability
- Pre-configured connections in DbGate

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
The following ports are automatically forwarded:

**Sample Applications:**
- **5000**: OpenFeed HTTP
- **5001**: OpenFeed HTTPS
- **5002**: AuthenticatedFeed HTTP
- **5003**: AuthenticatedFeed HTTPS

**Databases:**
- **1433**: SQL Server
- **3306**: MySQL
- **5432**: PostgreSQL
- **3000**: DbGate (Database Management UI)

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
1. Pull the .NET 10.0 SDK image and database images
2. Start SQL Server, MySQL, and PostgreSQL containers
3. Install all configured features (Git, Python, Node.js)
4. Run the post-create script to:
   - Install Python dependencies for MkDocs
   - Install Entity Framework Core tools
   - Restore NuGet packages
   - Build the solution

This process may take several minutes on the first run.

## Database Connection Strings

The following environment variables are automatically configured for connecting to the databases:

```bash
# SQL Server
SQLSERVER_CONNECTION="Server=sqlserver;Database=AvantiPointPackages;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"

# MySQL
MYSQL_CONNECTION="Server=mysql;Port=3306;Database=AvantiPointPackages;User=root;Password=YourStrong@Passw0rd;"

# PostgreSQL
POSTGRES_CONNECTION="Host=postgres;Port=5432;Database=AvantiPointPackages;Username=postgres;Password=YourStrong@Passw0rd;"
```

### Connecting from Host Machine

To connect from tools on your host machine (SQL Server Management Studio, DBeaver, etc.), use `localhost` with the appropriate port:

- **SQL Server**: `localhost:1433` (User: `sa`, Password: `YourStrong@Passw0rd`)
- **MySQL**: `localhost:3306` (User: `root`, Password: `YourStrong@Passw0rd`)
- **PostgreSQL**: `localhost:5432` (User: `postgres`, Password: `YourStrong@Passw0rd`)

⚠️ **Security Note**: These credentials are for development only. Never use them in production.

## DbGate Database Management

DbGate is a web-based database management tool that's automatically configured with connections to all three databases.

### Accessing DbGate

1. Once the dev container is running, DbGate will be available at: http://localhost:3000
2. All database connections are pre-configured and ready to use
3. No additional setup required!

### Features

- **Query Editor**: Write and execute SQL queries with syntax highlighting
- **Data Browser**: View and edit table data with filtering and sorting
- **Schema Designer**: Visualize database schemas and relationships
- **Import/Export**: Import and export data in various formats (CSV, JSON, Excel)
- **Multi-Database Support**: Switch between SQL Server, MySQL, and PostgreSQL seamlessly
- **Dark/Light Theme**: Customizable interface

### Pre-configured Connections

The following connections are automatically available:
- **SQL Server** - AvantiPointPackages database
- **MySQL** - AvantiPointPackages database
- **PostgreSQL** - AvantiPointPackages database

You can also add additional databases or modify existing connections through the DbGate UI.

### Data Persistence

Database data is persisted in Docker volumes, so your data survives container restarts:
- `sqlserver-data`
- `mysql-data`
- `postgres-data`

To reset a database, remove its volume:
```bash
docker volume rm avantipoint.packages_sqlserver-data
docker volume rm avantipoint.packages_mysql-data
docker volume rm avantipoint.packages_postgres-data
```

To reset DbGate settings and connections:
```bash
docker volume rm avantipoint.packages_dbgate-data
```

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
- `SQLSERVER_CONNECTION`: SQL Server connection string
- `MYSQL_CONNECTION`: MySQL connection string
- `POSTGRES_CONNECTION`: PostgreSQL connection string

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

### Database connection issues
- Ensure all database containers are healthy: `docker ps`
- Check database logs: `docker logs <container-name>`
- Wait for health checks to pass (may take 10-30 seconds on first start)
- Verify connection strings in environment variables

## More Information

- [VS Code Dev Containers Documentation](https://code.visualstudio.com/docs/devcontainers/containers)
- [AvantiPoint Packages Documentation](https://avantipoint.github.io/avantipoint.packages/)
