using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AvantiPoint.Packages.Registry.Native;

internal sealed class NativeStorageValidationService(IServiceScopeFactory scopes) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Storage discovery/providers are scoped. Do not resolve them from an
        // options validator or another singleton's root service provider.
        using var scope = scopes.CreateScope();
        if (scope.ServiceProvider.GetRequiredService<IStorageService>() is not IStreamingStorageService)
            throw new InvalidOperationException("Native feeds require an IStreamingStorageService provider.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
