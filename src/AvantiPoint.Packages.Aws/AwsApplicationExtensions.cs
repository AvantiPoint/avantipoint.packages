using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using AvantiPoint.Packages.Aws;
using AvantiPoint.Packages.Aws.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    /// <summary>
    /// Extension methods for adding AWS S3 Storage support.
    /// Supports both auto-discovery (configuration-based) and explicit registration modes.
    /// </summary>
    public static class AwsApplicationExtensions
    {
        /// <summary>
        /// Adds AWS S3 Storage support for auto-discovery mode.
        /// The storage will be configured from <see cref="S3StorageOptions"/> in configuration.
        /// </summary>
        public static NuGetApiOptions AddAwsS3Storage(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<S3StorageOptions>(nameof(PackageFeedOptions.Storage));
            // Runs after the named connection string is resolved into ConnectionString.
            options.Services.PostConfigure<S3StorageOptions>(o => o.ApplyConnectionString());

            options.Services.AddTransient<S3StorageService>();
            options.Services.AddScoped<IStorageServiceProvider, S3StorageServiceProvider>();

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

        /// <summary>
        /// Adds AWS S3 Storage support for auto-discovery mode with custom options configuration.
        /// </summary>
        public static NuGetApiOptions AddAwsS3Storage(this NuGetApiOptions options, Action<S3StorageOptions> configure)
        {
            options.AddAwsS3Storage();
            options.Services.Configure(configure);
            return options;
        }

        /// <summary>
        /// Adds AWS S3 Storage support with explicit client registration.
        /// This allows full control over the AmazonS3Client configuration.
        /// The provider will use the explicitly registered client instead of creating one from options.
        /// </summary>
        /// <param name="options">The NuGet API options.</param>
        /// <param name="s3Client">The pre-configured AmazonS3Client instance.</param>
        /// <returns>The NuGet API options for chaining.</returns>
        public static NuGetApiOptions AddAwsS3Storage(
            this NuGetApiOptions options,
            AmazonS3Client s3Client)
        {
            // Register the client explicitly
            options.Services.AddSingleton(s3Client);

            // Register the storage service and provider
            options.Services.AddTransient<S3StorageService>();
            options.Services.AddScoped<IStorageServiceProvider, S3StorageServiceProvider>();

            return options;
        }

        /// <summary>
        /// Adds AWS S3 Storage support with explicit client factory registration.
        /// This allows full control over how the AmazonS3Client is created.
        /// </summary>
        /// <param name="options">The NuGet API options.</param>
        /// <param name="s3ClientFactory">Factory function to create the AmazonS3Client.</param>
        /// <returns>The NuGet API options for chaining.</returns>
        public static NuGetApiOptions AddAwsS3Storage(
            this NuGetApiOptions options,
            Func<IServiceProvider, AmazonS3Client> s3ClientFactory)
        {
            // Register the client factory
            options.Services.AddSingleton(s3ClientFactory);

            // Register the storage service and provider
            options.Services.AddTransient<S3StorageService>();
            options.Services.AddScoped<IStorageServiceProvider, S3StorageServiceProvider>();

            return options;
        }

        /// <summary>
        /// Registers AWS S3 Storage provider for auto-discovery mode.
        /// The provider will be selected based on Storage:Type configuration.
        /// Does not register the storage service - it will be created on-demand by the provider.
        /// </summary>
        public static NuGetApiOptions AutoDiscoverAwsS3Storage(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<S3StorageOptions>(nameof(PackageFeedOptions.Storage));
            // Runs after the named connection string is resolved into ConnectionString.
            options.Services.PostConfigure<S3StorageOptions>(o => o.ApplyConnectionString());

            options.Services.AddTransient<S3StorageService>();
            options.Services.AddScoped<IStorageServiceProvider, S3StorageServiceProvider>();

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
    }
}