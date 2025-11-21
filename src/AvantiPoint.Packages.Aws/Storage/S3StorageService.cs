using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Aws
{
    public class S3StorageService : IStorageService
    {
        private const string Separator = "/";
        private readonly string _bucket;
        private readonly string _prefix;
        private readonly AmazonS3Client _client;

        public async IAsyncEnumerable<StorageFileInfo> ListFilesAsync(
            string prefix,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = string.IsNullOrEmpty(prefix)
                    ? _prefix
                    : _prefix + prefix.TrimStart('/').Replace("\\", Separator)
            };

            ListObjectsV2Response response;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                response = await _client.ListObjectsV2Async(request, cancellationToken);
                foreach (var obj in response.S3Objects)
                {
                    var key = obj.Key;
                    if (!string.IsNullOrEmpty(_prefix) && key.StartsWith(_prefix, StringComparison.Ordinal))
                    {
                        key = key.Substring(_prefix.Length);
                    }

                    yield return new StorageFileInfo(this, key, obj.LastModified);
                }

                request.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated ?? false);
        }

        public S3StorageService(IOptionsSnapshot<S3StorageOptions> options, AmazonS3Client client)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _bucket = options.Value.Bucket;
            _prefix = options.Value.Prefix;
            _client = client ?? throw new ArgumentNullException(nameof(client));

            if (!string.IsNullOrEmpty(_prefix) && !_prefix.EndsWith(Separator))
                _prefix += Separator;
        }

        private string PrepareKey(string path)
        {
            return _prefix + path.Replace("\\", Separator);
        }

        public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
        {
            var stream = new MemoryStream();

            try
            {
                using (var response = await _client.GetObjectAsync(_bucket, PrepareKey(path), cancellationToken))
                {
                    await response.ResponseStream.CopyToAsync(stream, cancellationToken);
                }

                stream.Seek(0, SeekOrigin.Begin);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                stream.Dispose();
                throw new FileNotFoundException($"Object '{path}' not found in bucket '{_bucket}'.", ex);
            }
            catch
            {
                stream.Dispose();
                throw;
            }

            return stream;
        }

        public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
        {
            var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = PrepareKey(path)
            });

            return Task.FromResult(new Uri(url));
        }

        public async Task<StoragePutResult> PutAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            // TODO: Uploads should be idempotent. This should fail if and only if the blob
            // already exists but has different content.

            using (var seekableContent = new MemoryStream())
            {
                await content.CopyToAsync(seekableContent, 4096, cancellationToken);

                seekableContent.Seek(0, SeekOrigin.Begin);

                await _client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = PrepareKey(path),
                    InputStream = seekableContent,
                    ContentType = contentType,
                    AutoResetStreamPosition = false,
                    AutoCloseStream = false
                }, cancellationToken);
            }

            return StoragePutResult.Success;
        }

        public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            await _client.DeleteObjectAsync(_bucket, PrepareKey(path), cancellationToken);
        }
    }
}
