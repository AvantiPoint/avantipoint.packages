using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Database.SqlServer;

internal class SqlServerContextServiceProvider(IServiceProvider services)
    : DatabaseContextServiceProvider<SqlServerContext>(services), IContextServiceProvider
{
    public override string Name => DatabaseProviderNames.SqlServer;

    protected override void ConfigureBuilder(IServiceProvider services, DbContextOptionsBuilder<SqlServerContext> builder)
    {
        var options = services.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString must be configured for SqlServer.");
        }

        builder.UseSqlServer(options.ConnectionString);
    }
}