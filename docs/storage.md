---
id: storage
title: Storage Configuration
sidebar_label: Storage
sidebar_position: 6
---

AvantiPoint Packages stores the actual package files (`.nupkg`) and symbol files (`.snupkg`) using a configured storage provider. The database stores metadata only.

## Supported Storage Providers

1. **File Storage** - Local file system or network share
2. **Azure Blob Storage** - Microsoft Azure cloud storage
3. **AWS S3** - Amazon Web Services cloud storage

## File Storage

The default storage provider saves packages to the local file system or a network share.

### Configuration

**appsettings.json**:

```json
{
  "Storage": {
    "Type": "FileStorage",
    "Path": "App_Data"
  }
}
```

**Program.cs**:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
});
```

### Path Options

The path can be:

1. **Relative** - Relative to the application directory:
   ```json
   { "Path": "App_Data" }
   ```

2. **Absolute** - Full path:
   ```json
   { "Path": "/var/packages" }
   ```
   or on Windows:
   ```json
   { "Path": "D:\\PackageStorage" }
   ```

3. **Network Share** - UNC path (Windows):
   ```json
   { "Path": "\\\\server\\share\\packages" }
   ```

### Directory Structure

File storage creates this structure:

```
App_Data/
  packages/
    newtonsoft.json/
      12.0.1/
        newtonsoft.json.12.0.1.nupkg
    mypackage/
      1.0.0/
        mypackage.1.0.0.nupkg
  symbols/
    newtonsoft.json/
      12.0.1/
        newtonsoft.json.12.0.1.snupkg
```

### Notes

- Simple and works well for small to medium deployments
- For production, use fast SSD storage
- For multiple instances, use a network share or cloud storage
- Ensure proper backup procedures

## Azure Blob Storage

Store packages in Microsoft Azure Blob Storage for scalability and reliability.

### Package

First, add the Azure package:

```bash
dotnet add package AvantiPoint.Packages.Azure
```

### Configuration

**appsettings.json**:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net"
  }
}
```

Or use account name and access key separately:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "AccountName": "mystorageaccount",
    "AccessKey": "your-access-key-here"
  }
}
```

**Program.cs**:

```csharp
using AvantiPoint.Packages.Azure;

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddAzureBlobStorage();
});
```

### Container

The container name should be the name of the blob container you created in Azure Storage. The provider will create `packages/` and `symbols/` subdirectories within the container.

Create the container before running your application:

```bash
az storage container create --name packages --account-name myaccount
```

Or it will be created automatically on first use if the account has appropriate permissions.

### Authentication Options

#### Connection String

Most common, includes account name and key:

```json
{
  "Storage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net"
  }
}
```

#### Account Name and Key

Separate the account name and key:

```json
{
  "Storage": {
    "AccountName": "mystorageaccount",
    "AccessKey": "your-access-key"
  }
}
```

#### Managed Identity (Recommended for Azure)

When running in Azure App Service, use Managed Identity:

1. Enable System Assigned Managed Identity on your App Service
2. Grant the identity "Storage Blob Data Contributor" role on the storage account
3. Remove `ConnectionString` and `AccessKey` from config
4. Provide only the account name:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "AccountName": "mystorageaccount"
  }
}
```

### Notes

- Highly scalable and reliable
- Built-in redundancy (LRS, GRS, etc.)
- Can enable CDN for global distribution
- Pay for storage used and transactions

## AWS S3 Storage

Store packages in Amazon S3 for scalability and global reach.

### Package

First, add the AWS package:

```bash
dotnet add package AvantiPoint.Packages.Aws
```

### Configuration

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

### Bucket

Create the S3 bucket before running your application:

```bash
aws s3 mb s3://my-nuget-packages --region us-west-2
```

The `Prefix` is optional and creates a subdirectory within the bucket.

### Authentication Options

#### Instance Profile (Recommended for EC2)

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

Create an IAM role with S3 permissions and attach it to your EC2 instance.

#### Explicit Credentials

For local development or when not using instance profiles:

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

**Warning**: Don't commit credentials to source control. Use environment variables or secrets management.

#### Assume Role

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

### Required IAM Permissions

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

### Notes

- Highly scalable and durable (99.999999999% durability)
- Global availability with edge locations
- Can enable CloudFront CDN for faster downloads
- Pay for storage and data transfer

## Choosing a Storage Provider

| Feature | File Storage | Azure Blob | AWS S3 |
|---------|--------------|------------|--------|
| **Setup Complexity** | Simple | Medium | Medium |
| **Scalability** | Limited | Unlimited | Unlimited |
| **Multi-Instance** | Network share required | Yes | Yes |
| **Cost** | Server storage | Pay-as-you-go | Pay-as-you-go |
| **Best For** | Development, small deployments | Azure-hosted apps | AWS-hosted apps |

## Performance Tips

### Cloud Storage

- **Same Region**: Use the same region as your application server
- **CDN**: Enable Azure CDN or CloudFront for global distribution
- **Storage Tiers**: Use hot tier for frequently accessed packages

### File Storage

- **Fast Disks**: Use SSD storage for better performance
- **Dedicated Storage**: Use dedicated storage to avoid I/O contention
- **Backup**: Implement regular backup procedures

## Migrating Between Providers

To migrate packages from one storage provider to another:

1. Set up the new storage provider
2. Copy all files from the old storage to the new storage
3. Maintain the same directory structure
4. Update your configuration
5. Test thoroughly before switching in production

For file storage to cloud:
- Use Azure Storage Explorer or AWS CLI to upload files
- Maintain the folder structure (`packages/` and `symbols/`)

## See Also

- [Database Configuration](database.md) - Configure the metadata database
- [Configuration](configuration.md) - Overall configuration guide
- [Hosting](hosting.md) - Deployment scenarios