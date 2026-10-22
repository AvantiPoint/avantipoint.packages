using AvantiPoint.Feed.Platform.Storage;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class DigestPathValidationTests
{
    [Theory]
    [InlineData("sha256:../other-surface")]
    [InlineData("..:abcdef")]
    [InlineData("sha256:abc/def")]
    [InlineData("sha256:abc\\def")]
    [InlineData("sha256:%2fetc")]
    [InlineData("sha256:abc?token=x")]
    [InlineData("sha256:")]
    [InlineData("sha/256:abc")]
    [InlineData("sha256:abc\n")]
    public void UnsafeDigestsCannotBecomeStoragePaths(string digest) =>
        Assert.Throws<ArgumentException>(() => DigestBlobStore.ParseDigest(digest));

    [Fact]
    public async Task SplitDigestApiAlsoRejectsTraversalBeforeTouchingStorage()
    {
        var store = new DigestBlobStore(null!, "oci/");
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetAsync("sha256", "../other", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => store.PutAsync("..", "abc", Stream.Null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync("sha256", "a/b", TestContext.Current.CancellationToken));
    }
}
