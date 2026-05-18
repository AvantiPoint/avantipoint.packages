namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

internal static class DatabaseSchemaQueries
{
    public static string PackageIndexNamesSql(DatabaseProviderKind provider) => provider switch
    {
        DatabaseProviderKind.Sqlite =>
            "SELECT name AS Name FROM sqlite_master WHERE type='index' AND name LIKE 'IX_Packages%'",
        DatabaseProviderKind.SqlServer =>
            @"SELECT name AS Name FROM sys.indexes
              WHERE object_id = OBJECT_ID('dbo.Packages')
              AND name LIKE 'IX_Packages%'",
        DatabaseProviderKind.PostgreSql =>
            @"SELECT indexname AS Name FROM pg_indexes
              WHERE tablename = 'Packages'
              AND indexname LIKE 'IX_Packages%'",
        DatabaseProviderKind.MySql =>
            @"SELECT index_name AS Name FROM information_schema.statistics
              WHERE table_schema = DATABASE()
              AND table_name = 'Packages'
              AND index_name LIKE 'IX_Packages%'",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    public static string ViewNamesSql(DatabaseProviderKind provider) => provider switch
    {
        DatabaseProviderKind.Sqlite =>
            "SELECT name AS Name FROM sqlite_master WHERE type='view'",
        DatabaseProviderKind.SqlServer =>
            "SELECT name AS Name FROM sys.views",
        DatabaseProviderKind.PostgreSql =>
            @"SELECT table_name AS Name FROM information_schema.views
              WHERE table_schema = 'public'",
        DatabaseProviderKind.MySql =>
            @"SELECT table_name AS Name FROM information_schema.views
              WHERE table_schema = DATABASE()",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };
}
