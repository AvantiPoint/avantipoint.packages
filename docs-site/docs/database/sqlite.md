---
id: sqlite
title: SQLite
sidebar_label: SQLite
sidebar_position: 2
---

SQLite is a lightweight, file-based database perfect for development and small deployments.

## Package

SQLite support is included in the core hosting package:

```bash
dotnet add package AvantiPoint.Packages.Database.Sqlite
```

## Configuration

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

## Connection String Options

```
Data Source=packages.db;Cache=Shared;Mode=ReadWriteCreate
```

- **Data Source**: Path to the database file (relative or absolute)
- **Cache**: `Shared` (default) or `Private`
- **Mode**: `ReadWriteCreate` (default), `ReadWrite`, or `ReadOnly`

## Notes

- The database file is created automatically if it doesn't exist
- Store the database file on reliable storage
- SQLite is single-writer, so it may not perform well under high concurrent write loads
- Great for simple deployments, local testing, and development
- Maximum database size: ~281 TB (in practice, limited by file system)
- Recommended for deployments with < 100 concurrent users

## Performance Considerations

### Write-Ahead Logging (WAL)

Enable WAL mode for better concurrent read performance:

```json
{
  "ConnectionStrings": {
    "Sqlite": "Data Source=packages.db;Mode=ReadWriteCreate;Pooling=true"
  }
}
```

Then set WAL mode at startup:

```csharp
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    using var db = scope.ServiceProvider.GetRequiredService<IContext>();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}
```

### Connection Pooling

Enable connection pooling for better performance:

```json
{
  "ConnectionStrings": {
    "Sqlite": "Data Source=packages.db;Pooling=true;Max Pool Size=25"
  }
}
```

## See Also

- [Database Overview](index.md)
- [SQL Server Configuration](sqlserver.md)
- [MySQL Configuration](mysql.md)
