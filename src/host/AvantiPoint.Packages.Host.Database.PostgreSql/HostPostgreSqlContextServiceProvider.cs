using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Host.Admin.Discovery;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Database.PostgreSql;

internal sealed class HostPostgreSqlContextServiceProvider(IServiceProvider services)
    : HostIdentityContextServiceProvider<HostPostgreSqlContext>(services)
{
    public override string Name => DatabaseProviderNames.PostgreSql;

    protected override void ConfigureBuilder(IServiceProvider sp, DbContextOptionsBuilder<HostPostgreSqlContext> builder)
    {
        builder.UseNpgsql(
            HostIdentityDatabaseConfiguration.GetConnectionString(sp),
            npgsql => npgsql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
    }
}
