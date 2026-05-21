using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Database.MySql;

public class HostMySqlContext(DbContextOptions<HostMySqlContext> options)
    : AbstractHostIdentityContext(options)
{
}
