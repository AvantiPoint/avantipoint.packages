using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System.Collections.Generic;
using Azure;
using Azure.Storage.Sas;

namespace AvantiPoint.Packages.Azure;

// See: https://github.com/NuGet/NuGetGallery/blob/master/src/NuGetGallery.Core/Services/CloudBlobCoreFileStorageService.cs
public class BlobStorageService : IStorageService, IStreamingStorageService
{
    private readonly BlobContainerClient _container;

    public async IAsyncEnumerable<StorageFileInfo> ListFilesAsync(
        string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        prefix ??= string.Empty;

        await foreach (var blob in _container.GetBlobsAsync(new() { Prefix = prefix }, cancellationToken: cancellationToken))
        {
            var lastModified = blob.Properties.LastModified;
            yield return new StorageFileInfo(this, blob.Name, lastModified);
        }
    }

    public BlobStorageService(BlobContainerClient container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken)
    {
        var client = _container
            .GetBlockBlobClient(path);
        if(await client.ExistsAsync(cancellationToken))
            return await client.OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);

        return Stream.Null;
    }

    public async Task CopyToAsync(string path, Stream destination, CancellationToken cancellationToken = default)
    {
        await using var source = await _container.GetBlockBlobClient(path)
            .OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);
        await source.CopyToAsync(destination, 81920, cancellationToken);
    }

    public async Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _container.GetBlockBlobClient(path).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            TransferOptions = new global::Azure.Storage.StorageTransferOptions
            {
                MaximumConcurrency = 1, InitialTransferSize = 4 * 1024 * 1024, MaximumTransferSize = 4 * 1024 * 1024,
            },
        }, cancellationToken);
    }

    public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken)
    {
        // TODO: Make expiry time configurable.
        var blob = _container.GetBlockBlobClient(path);

        var sasBuilder = new BlobSasBuilder(BlobContainerSasPermissions.Read, DateTimeOffset.Now.Add(TimeSpan.FromMinutes(5)))
        {
            Protocol = SasProtocol.Https,
            //IPRange = SasIPRange.Parse("")
        };
        return Task.FromResult(blob.GenerateSasUri(sasBuilder));
    }

    public async Task<StoragePutResult> PutAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var blob = _container.GetBlockBlobClient(path);
        //new global::Azure.Storage.Blobs.Models.BlobRequestConditions { }
        //var condition = AccessCondition.GenerateIfNotExistsCondition();

        //blob.Properties.ContentType = contentType;

        try
        {
            var options = new BlobUploadOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "ContentType", contentType }
                },
                AccessTier = AccessTier.Cool,
                //Conditions = new BlobRequestConditions
                //{
                //    IfMatch = new ETag()
                //}
            };
            await blob.UploadAsync(content, options, cancellationToken);

            return StoragePutResult.Success;
        }
        catch (RequestFailedException e) when (e.IsAlreadyExistsException())
        {
            using var targetStream = await blob.OpenReadAsync(new BlobOpenReadOptions(true), cancellationToken);
            content.Position = 0;
            return content.Matches(targetStream)
                ? StoragePutResult.AlreadyExists
                : StoragePutResult.Conflict;
        }
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        await _container
            .GetBlockBlobClient(path)
            .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }
}
