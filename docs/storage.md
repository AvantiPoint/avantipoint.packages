The normal file storage provider is FileStorage. This will save packages and symbols within the default App_Data directory by default. You can optionally specify a local path or network file share when running this On-Prem. 

## Azure Storage

When running this in Azure Storage you may want to use Azure Storage to decouple the packages stored from your server. To do this be sure to add the AvantiPoint.Packages.Azure package and register the Azure Storage provider.

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "my-nuget-feed",

    "ConnectionString": "your connection string",

    // Required if you do not provide the ConnectionString
    "AccountName": "my-storage-account-name",
    "AccessKey": "your access key"
  }
}
```

!!! note
    The container name should be the name of the Container that you have created in Azure Storage. It will create a packages and symbols sub-directory.

The AzureBlobStorage provider can be configured by either providing a ConnectionString or Account Name and Access Key.

## AWS S3 Bucket

You may optionally run this using AWS with an S3 Bucket for file storage. This provides a few authentication paths that you can use based on how you run your application and have setup Authentication. It's recommended that you are familiar with the Aws SDK and look at the AwsApplicationExtensions when setting this up as there are a few logical paths for creating the AmazonS3Client.

```json
{
  "Type": "AwsS3",
  "Region": "region name",
  "Bucket": "bucket name",
  "Prefix": "prefix",
  "AssumeRoleArn": "",
  "UseInstanceProfile": false,
  "SecretKey": ""
}
```