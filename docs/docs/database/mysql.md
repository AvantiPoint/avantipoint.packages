---
id: mysql
title: MySQL / MariaDB
sidebar_label: MySQL / MariaDB
sidebar_position: 4
---

MySQL and MariaDB are popular open-source databases that work great on Linux and cross-platform deployments.

## MySQL

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

## Connection String Options

### Basic

```
Server=localhost;Port=3306;Database=packages;User=nuget_user;Password=YourPassword;
```

### With SSL

```
Server=localhost;Database=packages;User=nuget_user;Password=YourPassword;SslMode=Required;
```

### Connection Pooling

```
Server=localhost;Database=packages;User=nuget_user;Password=YourPassword;Maximum Pool Size=100;Minimum Pool Size=10;
```

## Database Setup

### Create Database and User

```sql
-- Create database
CREATE DATABASE packages CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Create user
CREATE USER 'nuget_user'@'localhost' IDENTIFIED BY 'YourStrongPassword';

-- Grant permissions
GRANT ALL PRIVILEGES ON packages.* TO 'nuget_user'@'localhost';
FLUSH PRIVILEGES;
```

### For Remote Access

```sql
CREATE USER 'nuget_user'@'%' IDENTIFIED BY 'YourStrongPassword';
GRANT ALL PRIVILEGES ON packages.* TO 'nuget_user'@'%';
FLUSH PRIVILEGES;
```

## Cloud Deployments

### Amazon RDS for MySQL

```json
{
  "ConnectionStrings": {
    "MySql": "Server=myinstance.abc123.us-west-2.rds.amazonaws.com;Port=3306;Database=packages;User=admin;Password=YourPassword;SslMode=Required;"
  }
}
```

### Azure Database for MySQL

```json
{
  "ConnectionStrings": {
    "MySql": "Server=myserver.mysql.database.azure.com;Port=3306;Database=packages;User=admin@myserver;Password=YourPassword;SslMode=Required;"
  }
}
```

## Performance Tips

### InnoDB Buffer Pool

Adjust the InnoDB buffer pool size in `my.cnf` or `my.ini`:

```ini
[mysqld]
innodb_buffer_pool_size=2G
innodb_buffer_pool_instances=4
```

### Query Cache (MySQL < 8.0)

```ini
[mysqld]
query_cache_type=1
query_cache_size=64M
```

### Indexes

Migrations create appropriate indexes. Monitor slow queries:

```sql
-- Enable slow query log
SET GLOBAL slow_query_log = 'ON';
SET GLOBAL long_query_time = 1;

-- Check slow queries
SELECT * FROM mysql.slow_log;
```

### Replication

For high-availability and read scaling, set up MySQL replication:
- Master-Slave replication
- Multi-Source replication (MariaDB 10.0+)
- Group Replication (MySQL 8.0+)

## Troubleshooting

### "Access denied for user"

Check credentials and ensure the user has proper permissions.

### "Too many connections"

Increase `max_connections` in MySQL configuration:

```ini
[mysqld]
max_connections=500
```

### Character Encoding Issues

Ensure utf8mb4 is used:

```sql
ALTER DATABASE packages CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

## See Also

- [Database Overview](index.md)
- [SQLite Configuration](sqlite.md)
- [SQL Server Configuration](sqlserver.md)
