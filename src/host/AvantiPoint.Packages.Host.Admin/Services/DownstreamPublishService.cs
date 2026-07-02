using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Protocol;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed class DownstreamPublishService(
    IPackageStorageService packageStorageService,
    ISymbolStorageService symbolStorageService,
    ISecretProtector secretProtector,
    ILogger<DownstreamPublishService> logger) : IDownstreamPublishService
{
    public async Task<bool> PushPackageAsync(
        string packageId,
        NuGetVersion version,
        HostPublishTarget target,
        CancellationToken cancellationToken = default)
    {
        var client = new NuGetClient(target.PublishEndpoint);
        var apiToken = secretProtector.Unprotect(target.ApiToken);
        var result = await client.UploadPackageAsync(packageId, version, apiToken, packageStorageService, cancellationToken);
        if (!result)
        {
            logger.LogWarning("Failed to push {Package} {Version} to {Target}", packageId, version, target.Name);
        }

        return result;
    }

    public async Task<bool> PushSymbolsAsync(
        string packageId,
        NuGetVersion version,
        HostPublishTarget target,
        CancellationToken cancellationToken = default)
    {
        var client = new NuGetClient(target.PublishEndpoint);
        var apiToken = secretProtector.Unprotect(target.ApiToken);
        var result = await client.UploadSymbolsPackageAsync(packageId, version, apiToken, symbolStorageService, cancellationToken);
        if (!result)
        {
            logger.LogWarning("Failed to push symbols {Package} {Version} to {Target}", packageId, version, target.Name);
        }

        return result;
    }
}
