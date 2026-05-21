using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Host.Admin.Discovery;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Database.MySql;

internal sealed class HostMySqlContextServiceProvider(IServiceProvider services)
    : HostIdentityContextServiceProvider<HostMySqlContext>(services)
{
    public override string Name => DatabaseProviderNames.MySql;

    protected override void ConfigureBuilder(IServiceProvider sp, DbContextOptionsBuilder<HostMySqlContext> builder)
    {
        builder.UseMySQL(
            HostIdentityDatabaseConfiguration.GetConnectionString(sp),
            mySql => mySql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
    }
}
