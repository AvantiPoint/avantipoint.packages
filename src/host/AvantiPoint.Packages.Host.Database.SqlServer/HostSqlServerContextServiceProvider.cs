using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Host.Admin.Discovery;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Database.SqlServer;

internal sealed class HostSqlServerContextServiceProvider(IServiceProvider services)
    : HostIdentityContextServiceProvider<HostSqlServerContext>(services)
{
    public override string Name => DatabaseProviderNames.SqlServer;

    protected override void ConfigureBuilder(IServiceProvider sp, DbContextOptionsBuilder<HostSqlServerContext> builder)
    {
        builder.UseSqlServer(
            HostIdentityDatabaseConfiguration.GetConnectionString(sp),
            sql => sql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
    }
}
