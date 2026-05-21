using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace AvantiPoint.Packages.Host.Admin.Discovery;

public static class HostIdentityDatabaseConfiguration
{
    public static string GetConnectionString(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString must be configured for host identity.");
        }

        return options.ConnectionString;
    }
}
