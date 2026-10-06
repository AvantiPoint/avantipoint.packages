using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Native.Pub;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeIntegrityTests
{
    [Fact]
    public async Task CorruptPersistedBytesNeverCommitAnArtifactIdentity()
    {
        await using var host = await NativeTestHost.StartAsync();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var store = new NativeArtifactStore(context, new CorruptStorageFactory());
        var surface = new SurfaceContext("native-tests", FeedProtocol.Maven, "maven", null,
            "/maven", new Uri("https://registry.test/maven/"));
        await using var upload = await ArtifactUpload.ReadAsync(new MemoryStream("valid"u8.ToArray()), 100, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<IOException>(() => store.PutAsync(surface, "com/example/sdk/1.0.0/sdk-1.0.0.jar",
            "com.example:sdk", "1.0.0", "application/octet-stream", upload, null, TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.NativeArtifacts.CountAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.2", "1.0.0-alpha.10")]
    [InlineData("1.0.0-alpha.10", "1.0.0-beta")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.9.9", "1.10.0")]
    [InlineData("2147483647.0.0", "2147483648.0.0")]
    public void DartVersionsFollowSemverWithoutNugetIntegerLimits(string lower, string higher)
    {
        Assert.True(PubArchive.ValidVersion(lower));
        Assert.True(PubArchive.ValidVersion(higher));
        Assert.True(PubVersionComparer.Instance.Compare(lower, higher) < 0);
        Assert.True(PubVersionComparer.Instance.Compare(higher, lower) > 0);
    }

    [Theory]
    [InlineData("1.0.0-01")]
    [InlineData("01.0.0")]
    [InlineData("1.0")]
    [InlineData("1.0.0-")]
    public void InvalidDartVersionsAreRejected(string value) => Assert.False(PubArchive.ValidVersion(value));

    private sealed class CorruptStorageFactory : IStorageBackendFactory
    {
        public IPathBlobStore CreatePathStore(string subPrefix) => throw new NotSupportedException();
        public IDigestBlobStore CreateDigestStore(string subPrefix) => new CorruptBlobStore();
    }

    private sealed class CorruptBlobStore : IDigestBlobStore
    {
        public Task PutAsync(string algorithm, string hex, Stream content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Stream> GetAsync(string algorithm, string hex, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("partial")));
        public Task<bool> ExistsAsync(string algorithm, string hex, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task DeleteAsync(string algorithm, string hex, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
