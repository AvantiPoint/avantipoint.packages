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

                if (s3Options.UseInstanceProfile)
                {
                    var credentials = FallbackCredentialsFactory.GetCredentials();
                    return new AmazonS3Client(credentials, config);
                }

                if (!string.IsNullOrEmpty(s3Options.AssumeRoleArn))
                {
                    var credentials = FallbackCredentialsFactory.GetCredentials();
                    var assumedCredentials = AssumeRoleAsync(
                            credentials,
                            s3Options.AssumeRoleArn,
                            $"NuGetApiApplication-Session-{Guid.NewGuid()}")
                        .GetAwaiter()
                        .GetResult();

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

        private static async Task<AWSCredentials> AssumeRoleAsync(
            AWSCredentials credentials,
            string roleArn,
            string roleSessionName)
        {
            var assumedCredentials = new AssumeRoleAWSCredentials(credentials, roleArn, roleSessionName);
            var immutableCredentials = await credentials.GetCredentialsAsync();

            if (string.IsNullOrWhiteSpace(immutableCredentials.Token))
            {
                throw new InvalidOperationException($"Unable to assume role {roleArn}");
            }

            return assumedCredentials;
        }
    }
}