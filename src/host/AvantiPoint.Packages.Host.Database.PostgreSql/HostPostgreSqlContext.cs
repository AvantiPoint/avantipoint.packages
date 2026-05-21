using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Database.PostgreSql;

public class HostPostgreSqlContext(DbContextOptions<HostPostgreSqlContext> options)
    : AbstractHostIdentityContext(options)
{
}
