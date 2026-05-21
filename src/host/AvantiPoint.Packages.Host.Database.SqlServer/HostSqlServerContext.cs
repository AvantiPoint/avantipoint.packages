using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Database.SqlServer;

public class HostSqlServerContext(DbContextOptions<HostSqlServerContext> options)
    : AbstractHostIdentityContext(options)
{
}
