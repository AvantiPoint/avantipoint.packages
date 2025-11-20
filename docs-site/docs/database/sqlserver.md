---
id: sqlserver
title: SQL Server
sidebar_label: SQL Server
sidebar_position: 3
---

SQL Server is recommended for production Windows deployments.

## Package

```bash
dotnet add package AvantiPoint.Packages.Database.SqlServer
```

## Configuration

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

## Connection String Options

### Windows Authentication (Recommended)

```
Server=localhost;Database=AvantiPointPackages;Integrated Security=true;TrustServerCertificate=true
```

### SQL Authentication

```
Server=localhost;Database=AvantiPointPackages;User ID=sa;Password=YourPassword;TrustServerCertificate=true
```

### Azure SQL Database

```
Server=tcp:myserver.database.windows.net,1433;Database=Packages;User ID=admin;Password=YourPassword;Encrypt=true;Connection Timeout=30;
```

### Connection Pooling

```
Server=localhost;Database=packages;Integrated Security=true;Max Pool Size=100;Min Pool Size=10;
```

## Database Setup

### Create Database

```sql
CREATE DATABASE AvantiPointPackages;
GO

USE AvantiPointPackages;
GO
```

### Create Login and User

```sql
-- Create login
CREATE LOGIN nuget_user WITH PASSWORD = 'YourStrongPassword';
GO

-- Create user
USE AvantiPointPackages;
CREATE USER nuget_user FOR LOGIN nuget_user;
GO

-- Grant permissions
ALTER ROLE db_datareader ADD MEMBER nuget_user;
ALTER ROLE db_datawriter ADD MEMBER nuget_user;
ALTER ROLE db_ddladmin ADD MEMBER nuget_user;
GO
```

## Azure SQL Database

### Managed Identity (Recommended)

Use Managed Identity for secure, keyless authentication:

1. Enable System Assigned Managed Identity on your Azure App Service
2. Grant the identity access to Azure SQL:

   ```sql
   CREATE USER [your-app-service-name] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [your-app-service-name];
   ALTER ROLE db_datawriter ADD MEMBER [your-app-service-name];
   ALTER ROLE db_ddladmin ADD MEMBER [your-app-service-name];
   ```

3. Use this connection string:

   ```json
   {
     "ConnectionStrings": {
       "SqlServer": "Server=tcp:myserver.database.windows.net;Database=Packages;Authentication=Active Directory Default;"
     }
   }
   ```

### Pricing Tiers

- **Basic**: Development and testing (5 DTUs, 2 GB)
- **Standard**: Production workloads (10-3000 DTUs)
- **Premium**: High-performance, mission-critical (125-4000 DTUs)
- **Hyperscale**: Elastic scaling to 100+ TB

## Notes

- Create the database before first run, or use migrations
- Supports high concurrency and large datasets
- Built-in replication and high availability options
- Azure SQL Database fully supported
- Recommended for production Windows deployments
- Excellent tooling and management (SSMS, Azure Portal)

## Performance Tips

### Indexes

Migrations create appropriate indexes. Monitor query performance:

```sql
-- Find missing indexes
SELECT * FROM sys.dm_db_missing_index_details;

-- Find unused indexes
SELECT * FROM sys.dm_db_index_usage_stats;
```

### Query Store

Enable Query Store for performance monitoring:

```sql
ALTER DATABASE AvantiPointPackages SET QUERY_STORE = ON;
```

### Read Replicas

For read-heavy workloads, use Always On Availability Groups or Azure SQL read replicas.

## See Also

- [Database Overview](index.md)
- [SQLite Configuration](sqlite.md)
- [MySQL Configuration](mysql.md)
