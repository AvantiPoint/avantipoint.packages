using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Discovery;

public abstract class HostIdentityContextServiceProvider<TContext>(IServiceProvider services)
    : IHostIdentityContextServiceProvider
    where TContext : DbContext, IHostIdentityContext
{
    protected IServiceProvider Services { get; } = services;

    public abstract string Name { get; }

    protected abstract void ConfigureBuilder(IServiceProvider services, DbContextOptionsBuilder<TContext> builder);

    public IHostIdentityContext GetService()
    {
        var registered = Services.GetService<TContext>();
        if (registered != null)
        {
            return registered;
        }

        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        ConfigureBuilder(Services, optionsBuilder);
        return ActivatorUtilities.CreateInstance<TContext>(Services, optionsBuilder.Options);
    }

    public void ValidateConfiguration()
    {
        var registered = Services.GetService<TContext>();
        if (registered != null)
        {
            return;
        }

        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        ConfigureBuilder(Services, optionsBuilder);
    }
}

