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
    }
}