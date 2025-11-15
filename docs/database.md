# Database Configuration

AvantiPoint Packages requires a database to store package metadata, such as package IDs, versions, dependencies, and download counts. The database does not store the actual package files (that's handled by the [storage provider](storage.md)).

## Supported Database Providers

AvantiPoint Packages supports three database providers:

1. **SQLite** - Best for development and small deployments
2. **SQL Server** - Best for production Windows deployments
3. **MySQL / MariaDB** - Best for cross-platform production deployments

## SQLite

SQLite is a lightweight, file-based database perfect for development and small deployments.

### Package

SQLite support is included in the core hosting package:

```bash
dotnet add package AvantiPoint.Packages.Database.Sqlite
```

### Configuration

**appsettings.json**:

```json
{
  "Database": {
    "Type": "Sqlite"
  },
  "ConnectionStrings": {
    "Sqlite": "Data Source=packages.db"
  }
}
```

**Program.cs**:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddSqliteDatabase("Sqlite");
});
```

### Notes

- The database file is created automatically if it doesn't exist
- Store the database file on reliable storage
- SQLite is single-writer, so it may not perform well under high concurrent write loads
- Great for simple deployments, local testing, and development

## SQL Server

SQL Server is recommended for production Windows deployments.

### Package

```bash
dotnet add package AvantiPoint.Packages.Database.SqlServer
```

### Configuration

**appsettings.json**:

```json
{
  "Database": {
    "Type": "SqlServer"
  },
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=AvantiPointPackages;Integrated Security=true;"
  }
}
```

For Azure SQL or SQL Server with username/password:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=tcp:myserver.database.windows.net,1433;Database=Packages;User ID=admin;Password=YourPassword;Encrypt=true;"
  }
}
```

**Program.cs**:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddSqlServerDatabase("SqlServer");
});
```

### Notes

- Create the database before first run, or use migrations (see below)
- Supports high concurrency and large datasets
- Built-in replication and high availability options
- Azure SQL Database fully supported

## MySQL

MySQL is a popular open-source database that works great on Linux and cross-platform deployments.

### Package

```bash
dotnet add package AvantiPoint.Packages.Database.MySql
```

### Configuration

**appsettings.json**:

```json
{
  "Database": {
    "Type": "MySql"
  },
  "ConnectionStrings": {
    "MySql": "Server=localhost;Database=packages;User=root;Password=YourPassword;"
  }
}
```

**Program.cs**:

You must specify the server version for optimal performance:

```csharp
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

builder.Services.AddNuGetPackageApi(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("MySql");
    var serverVersion = ServerVersion.AutoDetect(connectionString);
    
    options.AddMySqlDatabase("MySql", serverVersion);
});
```

Or specify the version explicitly:

```csharp
var serverVersion = new MySqlServerVersion(new Version(8, 0, 33));
options.AddMySqlDatabase("MySql", serverVersion);
```

### Notes

- Works on Linux, Windows, and macOS
- Excellent performance for read-heavy workloads
- Good concurrent write performance
- Cloud versions available (Amazon RDS, Azure Database for MySQL, etc.)

## MariaDB

MariaDB is a MySQL fork with some enhancements. Configuration is similar to MySQL.

### Package

Same as MySQL:

```bash
dotnet add package AvantiPoint.Packages.Database.MySql
```

### Configuration

**appsettings.json**:

```json
{
  "Database": {
    "Type": "MariaDb"
  },
  "ConnectionStrings": {
    "MariaDb": "Server=localhost;Database=packages;User=root;Password=YourPassword;"
  }
}
```

**Program.cs**:

```csharp
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

builder.Services.AddNuGetPackageApi(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("MariaDb");
    var serverVersion = ServerVersion.AutoDetect(connectionString);
    
    options.AddMariaDbDatabase("MariaDb", serverVersion);
});
```

Or specify explicitly:

```csharp
var serverVersion = new MariaDbServerVersion(new Version(10, 11, 2));
options.AddMariaDbDatabase("MariaDb", serverVersion);
```

## Connection Strings

### By Name

Reference a connection string from the `ConnectionStrings` configuration section:

```csharp
options.AddSqlServerDatabase("SqlServer"); // Uses ConnectionStrings:SqlServer
```

### Inline

Pass the connection string directly:

```csharp
var connectionString = "Server=localhost;Database=packages;Integrated Security=true;";
options.AddSqlServerDatabase(connectionString);
```

### From Environment Variables

Use environment variables for sensitive data:

```bash
export ConnectionStrings__SqlServer="Server=localhost;Database=packages;..."
```

Or in Azure App Service, configure in Application Settings.

## Database Initialization

### Automatic Creation (Development)

In development, you can automatically create the database:

```csharp
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    using var db = scope.ServiceProvider.GetRequiredService<IContext>();
    db.Database.EnsureCreated();
}
```

**Warning**: `EnsureCreated()` doesn't use migrations and can't update the schema. For production, use migrations.

### Entity Framework Migrations (Production)

For production deployments, use EF Core migrations for better control:

1. **Add the EF Core tools**:

   ```bash
   dotnet tool install --global dotnet-ef
   ```

2. **Create a migration**:

   For SQLite:
   ```bash
   dotnet ef migrations add InitialCreate --context SqliteContext --project src/AvantiPoint.Packages.Database.Sqlite
   ```

   For SQL Server:
   ```bash
   dotnet ef migrations add InitialCreate --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer
   ```

   For MySQL:
   ```bash
   dotnet ef migrations add InitialCreate --context MySqlContext --project src/AvantiPoint.Packages.Database.MySql
   ```

3. **Apply the migration**:

   ```bash
   dotnet ef database update
   ```

4. **Or apply at startup**:

   ```csharp
   if (app.Environment.IsProduction())
   {
       using var scope = app.Services.CreateScope();
       using var db = scope.ServiceProvider.GetRequiredService<IContext>();
       db.Database.Migrate(); // Apply pending migrations
   }
   ```

## Environment-Specific Configuration

Use different databases for different environments:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    if (options.Environment.IsDevelopment())
    {
        options.AddSqliteDatabase("Sqlite");
    }
    else
    {
        options.AddSqlServerDatabase("SqlServer");
    }
});
```

Or use `appsettings.{Environment}.json` files:

**appsettings.Development.json**:
```json
{
  "Database": { "Type": "Sqlite" }
}
```

**appsettings.Production.json**:
```json
{
  "Database": { "Type": "SqlServer" }
}
```

## Performance Tips

### Connection Pooling

Connection pooling is enabled by default. For high-traffic applications, you can tune the pool size in your connection string:

**SQL Server**:
```
Server=localhost;Database=packages;Integrated Security=true;Max Pool Size=100;
```

**MySQL**:
```
Server=localhost;Database=packages;User=root;Password=pass;Maximum Pool Size=100;
```

### Indexes

The migrations create appropriate indexes. If you notice slow queries, check the execution plan and add custom indexes as needed.

### Read Replicas

For very high-traffic scenarios, consider using read replicas:
- SQL Server Always On
- MySQL replication
- Azure SQL Database read replicas

## Troubleshooting

### "Database does not exist"

Create the database manually or use `EnsureCreated()` in development.

### "Login failed for user"

Check your connection string credentials and ensure the user has appropriate permissions:
- SQL Server: `CREATE`, `ALTER`, `SELECT`, `INSERT`, `UPDATE`, `DELETE`
- MySQL: `CREATE`, `ALTER`, `SELECT`, `INSERT`, `UPDATE`, `DELETE`

### "Cannot open database"

Ensure the database server is running and accessible from your application server.

## See Also

- [Storage Configuration](storage.md) - Configure where packages are stored
- [Configuration](configuration.md) - Overall configuration guide
- [Hosting](hosting.md) - Deployment scenarios