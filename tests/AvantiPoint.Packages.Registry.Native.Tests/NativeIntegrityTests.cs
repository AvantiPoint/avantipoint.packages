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
        var store = new NativeArtifactStore(context, new CorruptStorage(), scope.ServiceProvider.GetRequiredService<IFeedRegistry>());
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
    [InlineData("1.0.0", "1.0.0+1")]
    [InlineData("1.0.0+2", "1.0.0+10")]
    [InlineData("1.0.0+2.1", "1.0.0+2.10")]
    [InlineData("1.0.0-dev+2", "1.0.0-dev+10")]
    public void DartVersionsFollowSemverWithoutNugetIntegerLimits(string lower, string higher)
    {
        Assert.True(PubArchive.ValidVersion(lower));
        Assert.True(PubArchive.ValidVersion(higher));
        Assert.True(PubVersionComparer.Instance.Compare(lower, higher) < 0);
        Assert.True(PubVersionComparer.Instance.Compare(higher, lower) > 0);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0-")]
    public void InvalidDartVersionsAreRejected(string value) => Assert.False(PubArchive.ValidVersion(value));

    [Fact]
    public void PubAcceptsLeadingZeroAliasesLikeTheNativeParser()
    {
        Assert.True(PubArchive.ValidVersion("01.02.03-01.dev+pre.02"));
        Assert.Equal("1.2.3-1.dev+pre.2", PubVersionComparer.Canonicalize("01.02.03-01.dev+pre.02"));
        Assert.Equal(0, PubVersionComparer.Instance.Compare("01.02.03-01.dev+pre.02", "1.2.3-1.dev+pre.2"));
    }

    [Fact]
    public async Task MavenOrderingMatchesPublishedMavenReferenceMatrix()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "maven-3.9.11-order.json"), TestContext.Current.CancellationToken);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var versions = document.RootElement.GetProperty("versions").EnumerateArray().Select(v => v.GetString()!).ToArray();
        var matrix = document.RootElement.GetProperty("comparisons").GetString()!;
        for (var i = 0; i < versions.Length; i++)
            for (var j = 0; j < versions.Length; j++)
            {
                var expected = matrix[i * versions.Length + j] switch { '<' => -1, '>' => 1, _ => 0 };
                var actual = Math.Sign(AvantiPoint.Packages.Registry.Native.Maven.MavenVersionComparer.Instance.Compare(versions[i], versions[j]));
                Assert.True(expected == actual, $"{versions[i]} vs {versions[j]}: expected {expected}, got {actual}.");
            }
    }

    private sealed class CorruptStorage : IStorageService, IStreamingStorageService
    {
        public Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CopyToAsync(string path, Stream destination, CancellationToken cancellationToken = default) =>
            destination.WriteAsync("wrong"u8.ToArray(), cancellationToken).AsTask();
        public Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException("Buffered reads must not be used.");
        public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoragePutResult> PutAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default) => throw new NotSupportedException("Buffered uploads must not be used.");
        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<StorageFileInfo> ListFilesAsync(string prefix, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
