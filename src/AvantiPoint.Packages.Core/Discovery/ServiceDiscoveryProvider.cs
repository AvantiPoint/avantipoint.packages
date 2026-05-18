using System;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Core.Discovery;

public abstract class ServiceDiscoveryProvider<TService, TImplementation>(IServiceProvider services)
    : IServiceDiscoveryProvider<TService>
    where TImplementation : TService
{
    protected IServiceProvider Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    public abstract string Name { get; }

    public virtual TService GetService() =>
        Services.GetRequiredService<TImplementation>();
    
    /// <summary>
    /// Default validation - ensures the service implementation can be resolved.
    /// Override to add provider-specific validation.
    /// </summary>
    public virtual void ValidateConfiguration()
    {
        // Ensure the service can be resolved
        _ = Services.GetRequiredService<TImplementation>();
    }
}
