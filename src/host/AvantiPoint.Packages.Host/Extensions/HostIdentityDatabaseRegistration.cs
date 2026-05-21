using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Host.Database.MySql;
using AvantiPoint.Packages.Host.Database.PostgreSql;
using AvantiPoint.Packages.Host.Database.Sqlite;
using AvantiPoint.Packages.Host.Database.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Extensions;

public static class HostIdentityDatabaseRegistration
{
    public static IServiceCollection AddHostIdentityDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseType = configuration.GetSection(nameof(PackageFeedOptions))
            .GetSection(nameof(DatabaseOptions))
            .GetValue<string>(nameof(DatabaseOptions.Type));

        if (string.IsNullOrWhiteSpace(databaseType))
        {
            databaseType = DatabaseProviderNames.Sqlite;
        }

        switch (databaseType.ToLowerInvariant())
        {
            case "sqlserver":
                services.AddHostSqlServerIdentityContext();
                break;
            case "mysql":
            case "mariadb":
                services.AddHostMySqlIdentityContext();
                break;
            case "postgresql":
            case "postgres":
                services.AddHostPostgreSqlIdentityContext();
                break;
            default:
                services.AddHostSqliteIdentityContext();
                break;
        }

        return services;
    }
}
