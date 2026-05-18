---
id: postgresql
title: PostgreSQL
sidebar_label: PostgreSQL
sidebar_position: 5
---

PostgreSQL is a powerful, open-source relational database that excels in production deployments across all platforms.

## Package

```bash
dotnet add package AvantiPoint.Packages.Database.PostgreSql
```

## Configuration

**appsettings.json**:

```json
{
  "Database": {
    "Type": "PostgreSql"
  },
  "ConnectionStrings": {
    "PostgreSql": "Host=localhost;Database=packages;Username=postgres;Password=YourPassword;"
  }
}
```

**Program.cs**:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddPostgreSqlDatabase("PostgreSql");
});
```

## Connection String Options

### Basic

```
Host=localhost;Port=5432;Database=packages;Username=postgres;Password=YourPassword;
```

### With SSL

```
Host=localhost;Database=packages;Username=postgres;Password=YourPassword;SSL Mode=Require;
```

### Connection Pooling

```
Host=localhost;Database=packages;Username=postgres;Password=YourPassword;Maximum Pool Size=100;Minimum Pool Size=10;
```

### Integrated Security (Windows)

```
Host=localhost;Database=packages;Integrated Security=true;
```

## Database Setup

### Create Database and User

```sql
-- Create database
CREATE DATABASE packages
    WITH ENCODING='UTF8'
    LC_COLLATE='en_US.UTF-8'
    LC_CTYPE='en_US.UTF-8';

-- Create user
CREATE USER nuget_user WITH PASSWORD 'YourStrongPassword';

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE packages TO nuget_user;

-- Connect to the database and grant schema permissions
\c packages
GRANT ALL ON SCHEMA public TO nuget_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO nuget_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO nuget_user;
```

### For Remote Access

Ensure PostgreSQL is configured to accept remote connections:

1. Edit `postgresql.conf`:
   ```ini
   listen_addresses = '*'
   ```

2. Edit `pg_hba.conf`:
   ```
   host    all             all             0.0.0.0/0               md5
   ```

3. Restart PostgreSQL service

## Cloud Deployments

### Amazon RDS for PostgreSQL

```json
{
  "ConnectionStrings": {
    "PostgreSql": "Host=myinstance.abc123.us-west-2.rds.amazonaws.com;Port=5432;Database=packages;Username=admin;Password=YourPassword;SSL Mode=Require;"
  }
}
```

### Azure Database for PostgreSQL

```json
{
  "ConnectionStrings": {
    "PostgreSql": "Host=myserver.postgres.database.azure.com;Port=5432;Database=packages;Username=admin@myserver;Password=YourPassword;SSL Mode=Require;"
  }
}
```

### Google Cloud SQL for PostgreSQL

```json
{
  "ConnectionStrings": {
    "PostgreSql": "Host=/cloudsql/project:region:instance;Database=packages;Username=postgres;Password=YourPassword;"
  }
}
```

## Performance Tips

### Connection Pooling

PostgreSQL connection pooling is handled by Npgsql. Configure pool size in the connection string:

```
Host=localhost;Database=packages;Username=postgres;Password=YourPassword;Maximum Pool Size=100;Minimum Pool Size=10;
```

### Shared Buffers

Adjust shared buffers in `postgresql.conf`:

```ini
shared_buffers = 256MB          # 25% of RAM for dedicated servers
effective_cache_size = 1GB       # 50-75% of RAM
maintenance_work_mem = 64MB
work_mem = 4MB
```

### Indexes

Migrations create appropriate indexes. Monitor slow queries:

```sql
-- Enable slow query logging
ALTER SYSTEM SET log_min_duration_statement = 1000; -- Log queries > 1 second
SELECT pg_reload_conf();

-- Check slow queries
SELECT * FROM pg_stat_statements ORDER BY total_time DESC LIMIT 10;
```

### Vacuum and Analyze

Schedule regular maintenance:

```sql
-- Manual vacuum and analyze
VACUUM ANALYZE;

-- Or configure autovacuum in postgresql.conf
autovacuum = on
autovacuum_max_workers = 3
```

### Read Replicas

For high-availability and read scaling, set up PostgreSQL streaming replication:
- Primary-Replica replication
- Logical replication (PostgreSQL 10+)
- Read replicas for query offloading

## Troubleshooting

### "Connection refused"

Check that PostgreSQL is running and accessible:

```bash
# Check if PostgreSQL is running
sudo systemctl status postgresql

# Check if port is listening
netstat -an | grep 5432
```

### "Password authentication failed"

Verify credentials and ensure the user exists:

```sql
SELECT usename FROM pg_user WHERE usename = 'nuget_user';
```

### "Database does not exist"

Create the database:

```sql
CREATE DATABASE packages;
```

### "Permission denied for schema public"

Grant schema permissions:

```sql
GRANT ALL ON SCHEMA public TO nuget_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO nuget_user;
```

### "Too many connections"

Increase `max_connections` in `postgresql.conf`:

```ini
max_connections = 200
```

Or use a connection pooler like PgBouncer.

## Notes

- Works on Linux, Windows, and macOS
- Excellent performance for both read and write workloads
- ACID compliant with strong consistency guarantees
- Advanced features: JSON support, full-text search, arrays, and more
- Cloud versions available (Amazon RDS, Azure Database, Google Cloud SQL)
- Recommended for production deployments requiring high concurrency
- Excellent tooling and ecosystem (pgAdmin, DBeaver, etc.)

## See Also

- [Database Overview](index.md)
- [SQLite Configuration](sqlite.md)
- [SQL Server Configuration](sqlserver.md)
- [MySQL Configuration](mysql.md)

