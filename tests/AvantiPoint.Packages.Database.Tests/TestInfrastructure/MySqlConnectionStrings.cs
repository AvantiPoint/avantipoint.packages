using MySqlConnector;

namespace AvantiPoint.Packages.Database.Tests.TestInfrastructure;

internal static class MySqlConnectionStrings
{
    /// <summary>
    /// Connection string for MySqlConnector (fixture admin: create/drop database).
    /// </summary>
    internal static string ConfigureForConnector(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowPublicKeyRetrieval = true,
            SslMode = MySqlSslMode.Preferred,
            ConnectionTimeout = 30
        };

        NormalizeServer(builder);
        return builder.ConnectionString;
    }

    /// <summary>
    /// Connection string for Oracle MySql.Data / EF Core UseMySQL.
    /// </summary>
    internal static string ConfigureForEntityFramework(string connectionString)
    {
        var source = new MySqlConnectionStringBuilder(connectionString);
        var builder = new global::MySql.Data.MySqlClient.MySqlConnectionStringBuilder
        {
            Server = source.Server,
            Port = (uint)source.Port,
            Database = source.Database,
            UserID = source.UserID,
            Password = source.Password,
            ConnectionTimeout = 30,
            SslMode = global::MySql.Data.MySqlClient.MySqlSslMode.Preferred
        };

        NormalizeServer(builder);
        return builder.ConnectionString;
    }

    private static void NormalizeServer(MySqlConnectionStringBuilder builder)
    {
        if (string.Equals(builder.Server, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            builder.Server = "127.0.0.1";
        }
    }

    private static void NormalizeServer(global::MySql.Data.MySqlClient.MySqlConnectionStringBuilder builder)
    {
        if (string.Equals(builder.Server, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            builder.Server = "127.0.0.1";
        }
    }
}
