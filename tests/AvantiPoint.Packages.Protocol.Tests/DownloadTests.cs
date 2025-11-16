using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol.Tests;

public class DownloadTests
{
    private const string FeedUrl = "https://api.nuget.org/v3/index.json";
    private const string PackageId = "Newtonsoft.Json";
    private static readonly NuGetVersion PackageVersion = new ("12.0.1");

    [Fact]
    public async Task DownloadPackage_ReturnsNonEmptyNupkgWithNuspec()
    {
        var client = new NuGetClient(FeedUrl);

        using var packageStream = await client.DownloadPackageAsync(PackageId, PackageVersion);

        Assert.NotNull(packageStream);
        Assert.True(packageStream.CanRead);

        // The HTTP response stream may be non-seekable; accessing Length can throw NotSupportedException.
        // Buffer the content to a MemoryStream to safely inspect length and ZIP contents.
        using var buffered = new MemoryStream();
        await packageStream.CopyToAsync(buffered);
        Assert.True(buffered.Length > 0, "Package stream should have content.");
        buffered.Position = 0;

        // Validate ZIP structure
        using var archive = new ZipArchive(buffered, ZipArchiveMode.Read, leaveOpen: true);
        Assert.True(archive.Entries.Count > 0, "Package archive should contain entries.");
        Assert.Contains(archive.Entries, e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DownloadPackageManifest_ContainsMatchingPackageId()
    {
        var client = new NuGetClient(FeedUrl);

        using var manifestStream = await client.DownloadPackageManifestAsync(PackageId, PackageVersion);

        Assert.NotNull(manifestStream);
        Assert.True(manifestStream.CanRead, "Manifest stream should be readable.");

        // The manifest stream may not be seekable (Length/Position can throw). Buffer to a MemoryStream.
        using var buffered = new MemoryStream();
        await manifestStream.CopyToAsync(buffered);
        Assert.True(buffered.Length > 0, "Buffered manifest should have content.");
        buffered.Position = 0;

        var doc = XDocument.Load(buffered);
        Assert.NotNull(doc.Root);
        Assert.Equal("package", doc.Root!.Name.LocalName);
        var ns = doc.Root!.GetDefaultNamespace();
        var idElement = doc.Root
            .Element(ns + "metadata")?
            .Element(ns + "id");

        Assert.NotNull(idElement);
        Assert.Equal(PackageId, idElement!.Value);
    }
}
