---
id: database
title: Database Configuration
sidebar_label: Overview
sidebar_position: 1
---

AvantiPoint Packages requires a database to store package metadata, such as package IDs, versions, dependencies, and download counts. The database does not store the actual package files (that's handled by the [storage provider](../storage/index.md)).

## Supported Database Providers

AvantiPoint Packages supports three database providers:

1. **[SQLite](sqlite.md)** - Best for development and small deployments
2. **[SQL Server](sqlserver.md)** - Best for production Windows deployments
3. **[MySQL / MariaDB](mysql.md)** - Best for cross-platform production deployments

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

- [SQLite Configuration](sqlite.md)
- [SQL Server Configuration](sqlserver.md)
- [MySQL Configuration](mysql.md)
- [Storage Configuration](../storage/index.md)
- [Configuration](../configuration.md)
