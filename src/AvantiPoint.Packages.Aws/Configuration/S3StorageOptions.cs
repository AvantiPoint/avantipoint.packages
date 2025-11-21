using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Aws
{
    public class S3StorageOptions
    {
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
    }
}