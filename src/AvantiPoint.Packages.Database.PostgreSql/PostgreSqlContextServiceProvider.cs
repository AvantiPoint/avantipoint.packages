using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Database.PostgreSql;

internal class PostgreSqlContextServiceProvider(IServiceProvider services)
    : DatabaseContextServiceProvider<PostgreSqlContext>(services), IContextServiceProvider
{
    public override string Name => DatabaseProviderNames.PostgreSql;

    protected override void ConfigureBuilder(IServiceProvider services, DbContextOptionsBuilder<PostgreSqlContext> builder)
    {
        var options = services.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString must be configured for PostgreSql.");
        }

        builder.UseNpgsql(options.ConnectionString);
    }
}

