using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AvantiPoint.Packages.Registry.Native.Pub;

namespace AvantiPoint.Packages.Registry.Native.Tests;

public sealed class PubArchiveLayoutTests
{
    [Theory]
    [InlineData("lib", true, "lib", false)]
    [InlineData("lib", false, "lib", true)]
    [InlineData("lib", false, "lib/example.dart", false)]
    [InlineData("lib/example.dart", false, "lib", false)]
    public async Task RejectsFileDirectoryConflictsInEitherOrder(string first, bool firstDirectory, string second, bool secondDirectory)
    {
        using var archive = Create((first, firstDirectory), (second, secondDirectory));
        await Assert.ThrowsAsync<InvalidDataException>(() => PubArchive.ReadAsync(archive, new(), Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AllowsAnExplicitDirectoryAfterItsChild()
    {
        using var archive = Create(("lib/example.dart", false), ("lib", true));
        Assert.Equal("example_sdk", (await PubArchive.ReadAsync(archive, new(), Xunit.TestContext.Current.CancellationToken)).Name);
    }

    private static MemoryStream Create(params (string Name, bool Directory)[] entries)
    {
        var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        using (var tar = new TarWriter(gzip, leaveOpen: true))
        {
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "pubspec.yaml")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes("name: example_sdk\nversion: 1.0.0\n")),
            });
            foreach (var entry in entries)
            {
                var type = entry.Directory ? TarEntryType.Directory : TarEntryType.RegularFile;
                var archiveEntry = new PaxTarEntry(type, entry.Name + (entry.Directory ? "/" : ""));
                if (!entry.Directory)
                    archiveEntry.DataStream = new MemoryStream(Encoding.UTF8.GetBytes("fixture"));
                tar.WriteEntry(archiveEntry);
            }
        }
        output.Position = 0;
        return output;
    }
}
