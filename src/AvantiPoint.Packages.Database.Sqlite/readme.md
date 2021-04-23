# AvantiPoint.Packages's SQLite Database Provider

This project contains AvantiPoint.Packages's SQLite database provider.

## Migrations

Add a migration with:

```
dotnet ef migrations add MigrationName --context SqliteContext --output-dir Migrations --startup-project ..\..\samples\OpenFeed\OpenFeed.csproj

dotnet ef database update --context SqliteContext
```