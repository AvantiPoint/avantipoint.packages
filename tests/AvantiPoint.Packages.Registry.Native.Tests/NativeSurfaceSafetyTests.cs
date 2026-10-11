using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Registry.Native.Pub;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class NativeSurfaceSafetyTests
{
    [Theory]
    [InlineData("maven", FeedProtocol.Maven)]
    [InlineData("swift", FeedProtocol.Swift)]
    [InlineData("pub", FeedProtocol.Pub)]
    public void ExistingOciSegmentsRemainValidUntilNativeSurfaceIsEnabled(string segment, FeedProtocol protocol)
    {
        var registry = new FeedRegistry(new FeedContext("test", "test", "test/"));
        var oci = new SurfaceRegistration("oci-" + segment, FeedProtocol.Oci, segment, "/" + segment, "Feed:Oci");
        var native = new SurfaceRegistration(segment, protocol, null, "/" + segment, "Feed:" + protocol);
        registry.Register(oci);
        Assert.Throws<InvalidOperationException>(() => registry.Register(native));
        var reverse = new FeedRegistry(new FeedContext("test", "test", "test/"));
        reverse.Register(native);
        Assert.Throws<InvalidOperationException>(() => reverse.Register(oci));
    }

    [Theory]
    [InlineData("plain scalar")]
    [InlineData("[name, version]")]
    [InlineData("null")]
    public async Task PubspecMustBeAnObject(string yaml)
    {
        using var archive = new MemoryStream();
        using (var gzip = new GZipStream(archive, CompressionMode.Compress, leaveOpen: true))
        using (var tar = new TarWriter(gzip, leaveOpen: true))
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "pubspec.yaml")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes(yaml)),
            });
        archive.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => PubArchive.ReadAsync(archive,
            new NativeRegistryOptions(), TestContext.Current.CancellationToken));
    }
}
