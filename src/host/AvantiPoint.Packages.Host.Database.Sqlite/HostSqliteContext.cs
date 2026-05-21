using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Database.Sqlite;

public class HostSqliteContext(DbContextOptions<HostSqliteContext> options)
    : AbstractHostIdentityContext(options)
{
}
