using System;
using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Aws
{
    public class S3StorageOptions : IConnectionStringOptions
    {
        /// <summary>
        /// A URI-style connection string, for example
        /// <c>s3://accessKey:secretKey@bucket?region=us-east-1</c>, or for an S3-compatible endpoint
        /// <c>s3://accessKey:secretKey@bucket?endpoint=http://localhost:9000&amp;forcePathStyle=true</c>.
        /// When supplied, its components populate the fields below.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The name of a connection string under the root <c>ConnectionStrings</c> section to use for
        /// <see cref="ConnectionString"/> (for example <c>ConnectionStrings__Storage</c>).
        /// </summary>
        public string ConnectionStringName { get; set; }

        [RequiredIf(nameof(SecretKey), null, IsInverted = true)]
        public string AccessKey { get; set; }

        [RequiredIf(nameof(AccessKey), null, IsInverted = true)]
        public string SecretKey { get; set; }

        [Required]
        public string Region { get; set; }

        [Required]
        public string Bucket { get; set; }

        public string Prefix { get; set; }

        public bool UseInstanceProfile { get; set; }

        public string AssumeRoleArn { get; set; }

        /// <summary>
        /// Custom service URL for S3-compatible providers (e.g., MinIO, LocalStack).
        /// If set, overrides the default AWS S3 endpoint.
        /// Example: "http://localhost:9000" for MinIO.
        /// </summary>
        public string ServiceUrl { get; set; }

        /// <summary>
        /// Force path-style addressing for S3-compatible providers.
        /// Required for most S3-compatible providers (MinIO, LocalStack, etc.).
        /// Default: false (uses virtual-hosted style for AWS S3).
        /// </summary>
        public bool ForcePathStyle { get; set; } = false;

        /// <summary>
        /// Populates the individual fields from <see cref="ConnectionString"/> when it is a URI-style
        /// value. Fields not present in the connection string are left unchanged. Called as a
        /// post-configure step after any named connection string has been resolved.
        /// </summary>
        public void ApplyConnectionString()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                return;
            }

            if (!ConnectionStringUri.TryParse(ConnectionString, out var uri))
            {
                throw new InvalidOperationException(
                    "The AWS S3 storage connection string must be a URI, for example 's3://accessKey:secretKey@bucket?region=us-east-1'.");
            }

            if (!string.IsNullOrEmpty(uri.UserName)) AccessKey = uri.UserName;
            if (!string.IsNullOrEmpty(uri.Password)) SecretKey = uri.Password;
            if (!string.IsNullOrEmpty(uri.Host)) Bucket = uri.Host;

            var endpoint = uri.GetString("endpoint") ?? uri.GetString("serviceUrl");
            if (!string.IsNullOrEmpty(endpoint)) ServiceUrl = endpoint;

            if (uri.GetString("region") is { Length: > 0 } region) Region = region;
            if (uri.GetBool("forcePathStyle") is { } forcePathStyle) ForcePathStyle = forcePathStyle;
            if (uri.GetBool("useInstanceProfile") is { } useInstanceProfile) UseInstanceProfile = useInstanceProfile;
            if (uri.GetString("assumeRoleArn") is { Length: > 0 } assumeRoleArn) AssumeRoleArn = assumeRoleArn;

            var prefix = uri.GetString("prefix") ?? uri.Path;
            if (!string.IsNullOrEmpty(prefix)) Prefix = prefix;

            // S3-compatible endpoints still require a region for the SDK; default when not supplied.
            if (string.IsNullOrEmpty(Region) && !string.IsNullOrEmpty(ServiceUrl))
            {
                Region = "us-east-1";
            }
        }
    }
}
