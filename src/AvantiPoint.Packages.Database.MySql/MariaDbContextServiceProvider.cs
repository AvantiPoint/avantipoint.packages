using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace AvantiPoint.Packages.Database.MySql;

internal class MariaDbContextServiceProvider(IServiceProvider services)
    : DatabaseContextServiceProvider<MySqlContext>(services), IContextServiceProvider
{
    public override string Name => DatabaseProviderNames.MariaDb;

    protected override void ConfigureBuilder(IServiceProvider services, DbContextOptionsBuilder<MySqlContext> builder)
    {
        var options = services.GetRequiredService<IOptionsSnapshot<MySqlDatabaseOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString must be configured for MariaDb.");
        }

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            throw new InvalidOperationException("Database:Version must be configured for MariaDb.");
        }

        var version = new MariaDbServerVersion(options.Version);
        builder.UseMySql(options.ConnectionString, version);
    }
}