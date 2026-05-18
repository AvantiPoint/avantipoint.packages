using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Database.Sqlite;

internal class SqliteContextServiceProvider(IServiceProvider services)
    : DatabaseContextServiceProvider<SqliteContext>(services), IContextServiceProvider
{
    public override string Name => DatabaseProviderNames.Sqlite;

    protected override void ConfigureBuilder(IServiceProvider services, DbContextOptionsBuilder<SqliteContext> builder)
    {
        var options = services.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString must be configured for Sqlite.");
        }

        builder.UseSqlite(options.ConnectionString);
    }
}


