using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Database.MySql;

internal class MySqlContextServiceProvider(IServiceProvider services)
    : DatabaseContextServiceProvider<MySqlContext>(services), IContextServiceProvider
{
    public override string Name => DatabaseProviderNames.MySql;

    protected override void ConfigureBuilder(IServiceProvider services, DbContextOptionsBuilder<MySqlContext> builder)
    {
        var options = services.GetRequiredService<IOptionsSnapshot<MySqlDatabaseOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString must be configured for MySql.");
        }

        builder.UseMySQL(options.ConnectionString);
    }
}
