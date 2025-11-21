using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using AvantiPoint.Packages.Aws;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    public static class AwsApplicationExtensions
    {
        public static NuGetApiOptions AddAwsS3Storage(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<S3StorageOptions>(nameof(PackageFeedOptions.Storage));

            options.Services.AddTransient<S3StorageService>();
            options.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<S3StorageService>());

            options.Services.AddProvider<IStorageService>((provider, config) =>
            {
                if (!config.HasStorageType("AwsS3")) return null;

                return provider.GetRequiredService<S3StorageService>();
            });

            options.Services.AddSingleton(provider =>
            {
                var s3Options = provider.GetRequiredService<IOptions<S3StorageOptions>>().Value;

                var config = new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region)
                };

                // Support S3-compatible providers (MinIO, LocalStack, etc.)
                if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
                {
                    config.ServiceURL = s3Options.ServiceUrl;
                    config.ForcePathStyle = s3Options.ForcePathStyle;
                }

                if (s3Options.UseInstanceProfile)
                {
                    // Rely on the default AWS credential chain (instance profile, env vars, shared config, etc.)
                    return new AmazonS3Client(config);
                }

                if (!string.IsNullOrEmpty(s3Options.AssumeRoleArn))
                {
                    // Start from either explicit access keys (if provided) or the default credential chain,
                    // then assume the configured role.
                    AWSCredentials baseCredentials;

                    if (!string.IsNullOrEmpty(s3Options.AccessKey))
                    {
                        baseCredentials = new BasicAWSCredentials(
                            s3Options.AccessKey,
                            s3Options.SecretKey);
                    }
                    else
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        baseCredentials = FallbackCredentialsFactory.GetCredentials();
#pragma warning restore CS0618
                    }

                    var assumedCredentials = new AssumeRoleAWSCredentials(
                        baseCredentials,
                        s3Options.AssumeRoleArn,
                        $"NuGetApiApplication-Session-{Guid.NewGuid()}");

                    return new AmazonS3Client(assumedCredentials, config);
                }

                if (!string.IsNullOrEmpty(s3Options.AccessKey))
                {
                    return new AmazonS3Client(
                        new BasicAWSCredentials(
                            s3Options.AccessKey,
                            s3Options.SecretKey),
                        config);
                }

                // Fall back to standard AWS credential resolution (env vars, shared config, etc.)
                return new AmazonS3Client(config);
            });

            return options;
        }

        public static NuGetApiOptions AddAwsS3Storage(this NuGetApiOptions options, Action<S3StorageOptions> configure)
        {
            options.AddAwsS3Storage();
            options.Services.Configure(configure);
            return options;
        }

    }
}