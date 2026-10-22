using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Services;

public sealed class BlobStoreExistenceTests
{
    [Theory]
    [InlineData(false, "sentinel", false)]
    [InlineData(true, "sentinel", false)]
    [InlineData(false, "null", false)]
    [InlineData(true, "null", false)]
    [InlineData(false, "file", false)]
    [InlineData(true, "file", false)]
    [InlineData(false, "directory", false)]
    [InlineData(true, "directory", false)]
    [InlineData(false, "empty", true)]
    [InlineData(true, "empty", true)]
    [InlineData(false, "content", true)]
    [InlineData(true, "content", true)]
    public async Task Exists_RespectsProviderMissingObjectContract(bool digest, string result, bool expected)
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => result switch
            {
                "sentinel" => Task.FromResult(Stream.Null),
                "null" => Task.FromResult<Stream>(null!),
                "file" => Task.FromException<Stream>(new FileNotFoundException()),
                "directory" => Task.FromException<Stream>(new DirectoryNotFoundException()),
                "empty" => Task.FromResult<Stream>(new MemoryStream()),
                _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])),
            });
        var exists = digest
            ? await new DigestBlobStore(storage.Object, "test").ExistsAsync("sha256", "abcd", TestContext.Current.CancellationToken)
            : await new PathBlobStore(storage.Object, "test").ExistsAsync("package.tgz", TestContext.Current.CancellationToken);
        Assert.Equal(expected, exists);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exists_DoesNotHideStorageFailures(bool digest)
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("storage unavailable"));
        await Assert.ThrowsAsync<IOException>(() => digest
            ? new DigestBlobStore(storage.Object, "test").ExistsAsync("sha256", "abcd", TestContext.Current.CancellationToken)
            : new PathBlobStore(storage.Object, "test").ExistsAsync("package.tgz", TestContext.Current.CancellationToken));
    }
}
