---
id: awss3
title: AWS S3 Storage
sidebar_label: AWS S3
sidebar_position: 4
---

Store packages in Amazon S3 for scalability and global reach.

## Package

First, add the AWS package:

```bash
dotnet add package AvantiPoint.Packages.Aws
```

## Configuration

**appsettings.json**:

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-west-2",
    "Bucket": "my-nuget-packages",
    "Prefix": "packages"
  }
}
```

**Program.cs**:

```csharp
using AvantiPoint.Packages.Aws;

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddAwsS3Storage();
});
```

## Bucket Setup

Create the S3 bucket before running your application:

```bash
aws s3 mb s3://my-nuget-packages --region us-west-2
```

Or using AWS Console:
1. Navigate to S3 service
2. Click "Create bucket"
3. Name: `my-nuget-packages`
4. Region: Choose appropriate region
5. Block all public access: **Enabled** (keep private)
6. Click "Create bucket"

### Prefix

The `Prefix` is optional and creates a subdirectory within the bucket:

```json
{
  "Storage": {
    "Prefix": "packages"
  }
}
```

This creates structure like: `s3://my-nuget-packages/packages/newtonsoft.json/...`

Useful when sharing a bucket with other applications.

## Authentication Options

### 1. Instance Profile (Recommended for EC2)

When running on EC2, use an instance profile:

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-west-2",
    "Bucket": "my-packages",
    "UseInstanceProfile": true
  }
}
```

**Setup:**

1. **Create IAM role** with S3 permissions (see below)
2. **Attach role to EC2 instance** when launching
3. **Omit credentials** from configuration

Benefits:
- No credentials to manage
- Automatic credential rotation
- Better security

### 2. Access Key and Secret Key

For local development or non-EC2 environments:

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-west-2",
    "Bucket": "my-packages",
    "AccessKey": "AKIAIOSFODNN7EXAMPLE",
    "SecretKey": "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
  }
}
```

**Warning**: Don't commit credentials to source control. Use environment variables or AWS Secrets Manager.

### 3. Assume Role

Use a role with temporary credentials:

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-west-2",
    "Bucket": "my-packages",
    "AssumeRoleArn": "arn:aws:iam::123456789012:role/MyRole"
  }
}
```

Useful for cross-account access or enhanced security.

### 4. AWS CLI Default Credentials

Use credentials from `~/.aws/credentials`:

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-west-2",
    "Bucket": "my-packages"
  }
}
```

AWS SDK automatically discovers credentials from:
1. Environment variables
2. AWS credentials file
3. IAM instance profile
4. ECS task role

## Required IAM Permissions

Your IAM user or role needs these S3 permissions:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
        "s3:GetObject",
        "s3:DeleteObject",
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::my-nuget-packages",
        "arn:aws:s3:::my-nuget-packages/*"
      ]
    }
  ]
}
```

### Create IAM Policy

```bash
aws iam create-policy \
  --policy-name NuGetS3Access \
  --policy-document file://s3-policy.json
```

### Attach Policy to Role

```bash
aws iam attach-role-policy \
  --role-name MyEC2Role \
  --policy-arn arn:aws:iam::123456789012:policy/NuGetS3Access
```

## Bucket Configuration

### Versioning

Enable versioning for package history and recovery:

```bash
aws s3api put-bucket-versioning \
  --bucket my-nuget-packages \
  --versioning-configuration Status=Enabled
```

### Encryption

Enable server-side encryption (SSE-S3):

```bash
aws s3api put-bucket-encryption \
  --bucket my-nuget-packages \
  --server-side-encryption-configuration '{
    "Rules": [{
      "ApplyServerSideEncryptionByDefault": {
        "SSEAlgorithm": "AES256"
      }
    }]
  }'
```

Or use AWS KMS for more control:

```json
{
  "Rules": [{
    "ApplyServerSideEncryptionByDefault": {
      "SSEAlgorithm": "aws:kms",
      "KMSMasterKeyID": "arn:aws:kms:us-west-2:123456789012:key/12345678-1234-1234-1234-123456789012"
    }
  }]
}
```

### Lifecycle Policies

Move old packages to cheaper storage classes:

```json
{
  "Rules": [
    {
      "Id": "MoveOldPackagesToIA",
      "Status": "Enabled",
      "Filter": {
        "Prefix": "packages/"
      },
      "Transitions": [
        {
          "Days": 90,
          "StorageClass": "STANDARD_IA"
        },
        {
          "Days": 180,
          "StorageClass": "GLACIER"
        }
      ]
    }
  ]
}
```

Apply policy:

```bash
aws s3api put-bucket-lifecycle-configuration \
  --bucket my-nuget-packages \
  --lifecycle-configuration file://lifecycle.json
```

## CloudFront CDN

Enable CloudFront for faster global downloads:

1. **Create CloudFront distribution**:
   ```bash
   aws cloudfront create-distribution \
     --origin-domain-name my-nuget-packages.s3.amazonaws.com \
     --default-root-object index.html
   ```

2. **Update S3 bucket policy** to allow CloudFront access:
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [{
       "Effect": "Allow",
       "Principal": {
         "AWS": "arn:aws:iam::cloudfront:user/CloudFront Origin Access Identity E1234567890ABC"
       },
       "Action": "s3:GetObject",
       "Resource": "arn:aws:s3:::my-nuget-packages/*"
     }]
   }
   ```

3. **Configure NuGet clients** to use CloudFront URL

Benefits:
- Faster downloads worldwide
- Reduced S3 data transfer costs
- DDoS protection

## Performance Tips

### Same Region

Use the same region as your application:

```json
{
  "Storage": {
    "Region": "us-west-2"
  }
}
```

Check your EC2 instance region:

```bash
ec2-metadata --availability-zone
```

### Transfer Acceleration

Enable S3 Transfer Acceleration for faster uploads over long distances:

```bash
aws s3api put-bucket-accelerate-configuration \
  --bucket my-nuget-packages \
  --accelerate-configuration Status=Enabled
```

Update configuration:

```json
{
  "Storage": {
    "UseAccelerateEndpoint": true
  }
}
```

### Multipart Upload

For packages > 100 MB, use multipart upload (automatic in AWS SDK).

## Storage Classes

- **STANDARD**: Default, frequently accessed (use for active packages)
- **STANDARD_IA**: Infrequent access, lower storage cost
- **GLACIER**: Archive, very low cost, slow retrieval
- **GLACIER_DEEP_ARCHIVE**: Lowest cost, 12-hour retrieval

Use **STANDARD** for package feeds.

## Monitoring

Monitor S3 metrics in CloudWatch:

- **BucketSizeBytes**: Total storage used
- **NumberOfObjects**: Total object count
- **AllRequests**: Request count
- **4xxErrors / 5xxErrors**: Error rates

Set up CloudWatch alarms for:
- High error rates
- Unusual request patterns
- Storage cost approaching budget

Enable S3 access logging:

```bash
aws s3api put-bucket-logging \
  --bucket my-nuget-packages \
  --bucket-logging-status '{
    "LoggingEnabled": {
      "TargetBucket": "my-logs-bucket",
      "TargetPrefix": "s3-access-logs/"
    }
  }'
```

## Cost Optimization

### Right-Size Storage Class

Use lifecycle policies to move old packages to cheaper storage.

### Optimize Requests

- Cache frequently accessed packages
- Use CloudFront to reduce S3 requests
- Batch operations when possible

### Reserved Capacity

Purchase S3 storage for predictable workloads (not available for S3, but consider Savings Plans for EC2 costs).

### Delete Unused Packages

Periodically clean up:
- Unlisted packages
- Old prerelease versions
- Orphaned files

## Troubleshooting

### "Access Denied" Errors

Check:
- IAM permissions are correct
- Bucket policy allows access
- Instance profile is attached (EC2)
- Credentials are valid

### "NoSuchBucket" Errors

Check:
- Bucket name is correct
- Bucket exists in specified region
- Region in configuration matches bucket region

### Slow Performance

Consider:
- Use same region as application
- Enable Transfer Acceleration
- Enable CloudFront CDN
- Check network bandwidth

### Authentication Issues

Verify:
- Access key and secret key are correct
- Instance profile is attached to EC2 instance
- IAM role has proper permissions
- Credentials are not expired

## Migration from File Storage

To migrate from file storage:

1. **Install AWS CLI** and configure credentials
2. **Sync files** to S3:
   ```bash
   aws s3 sync /local/path/to/packages s3://my-nuget-packages/packages/
   aws s3 sync /local/path/to/symbols s3://my-nuget-packages/symbols/
   ```
3. **Update configuration** to use S3
4. **Test thoroughly** before switching production

## Cross-Region Replication

For disaster recovery or multi-region deployment:

1. **Enable versioning** on source and destination buckets
2. **Create replication rule**:
   ```bash
   aws s3api put-bucket-replication \
     --bucket my-nuget-packages \
     --replication-configuration file://replication.json
   ```

Example `replication.json`:

```json
{
  "Role": "arn:aws:iam::123456789012:role/S3ReplicationRole",
  "Rules": [{
    "Status": "Enabled",
    "Priority": 1,
    "Filter": {},
    "Destination": {
      "Bucket": "arn:aws:s3:::my-packages-backup",
      "ReplicationTime": {
        "Status": "Enabled",
        "Time": {
          "Minutes": 15
        }
      }
    }
  }]
}
```

## See Also

- [Storage Overview](index.md)
- [File Storage Configuration](filestorage.md)
- [Azure Blob Storage Configuration](azureblob.md)
- [AWS S3 Documentation](https://docs.aws.amazon.com/s3/)
