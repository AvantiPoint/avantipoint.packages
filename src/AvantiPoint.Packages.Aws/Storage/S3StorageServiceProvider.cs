using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AvantiPoint.Packages.Aws;

namespace AvantiPoint.Packages.Aws.Storage;

internal class S3StorageServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IStorageService, S3StorageService>(services), IStorageServiceProvider
{
    public override string Name => StorageProviderNames.AwsS3;

    public override void ValidateConfiguration()
    {
        var options = Services.GetRequiredService<IOptions<S3StorageOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.Bucket))
        {
            throw new InvalidOperationException("AWS S3 storage requires a bucket name.");
        }

        if (string.IsNullOrWhiteSpace(options.Region))
        {
            throw new InvalidOperationException("AWS S3 storage requires a region.");
        }

        var hasAccessKeys = !string.IsNullOrWhiteSpace(options.AccessKey) &&
            !string.IsNullOrWhiteSpace(options.SecretKey);

        if (!options.UseInstanceProfile &&
            string.IsNullOrWhiteSpace(options.AssumeRoleArn) &&
            !hasAccessKeys)
        {
            throw new InvalidOperationException(
                "AWS S3 storage requires credentials (AccessKey/SecretKey, AssumeRoleArn, or UseInstanceProfile).");
        }
    }
}