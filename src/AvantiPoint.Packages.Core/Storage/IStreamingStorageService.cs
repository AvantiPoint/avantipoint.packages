using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Bounded-memory transfers for large content-addressed artifacts. Implementations
/// must not stage an entire object in a MemoryStream. Caller owns both streams.
/// Upload may replace bytes at its digest key; logical release immutability is
/// enforced by the native artifact catalog after persisted-byte verification.
/// </summary>
public interface IStreamingStorageService
{
    Task CopyToAsync(string path, Stream destination, CancellationToken cancellationToken = default);
    Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default);
}
