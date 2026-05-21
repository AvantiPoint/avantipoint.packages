using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Host.Admin.Discovery;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Database.Sqlite;

internal sealed class HostSqliteContextServiceProvider(IServiceProvider services)
    : HostIdentityContextServiceProvider<HostSqliteContext>(services)
{
    public override string Name => DatabaseProviderNames.Sqlite;

    protected override void ConfigureBuilder(IServiceProvider sp, DbContextOptionsBuilder<HostSqliteContext> builder)
    {
        builder.UseSqlite(
            HostIdentityDatabaseConfiguration.GetConnectionString(sp),
            sql => sql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
    }
}
