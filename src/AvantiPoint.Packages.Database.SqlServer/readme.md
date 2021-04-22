# AvantiPoint.Packages's SQL Server Database Provider

This project contains AvantiPoint.Packages's Microsoft SQL Server database provider.

## Migrations

Add a migration with:

```
dotnet ef migrations add MigrationName --context SqlServerContext --output-dir Migrations --startup-project ..\..\samples\AuthenticatedFeed\AuthenticatedFeed.csproj

dotnet ef database update --context SqlServerContext
```
