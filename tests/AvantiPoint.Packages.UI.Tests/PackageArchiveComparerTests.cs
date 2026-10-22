using System.IO.Compression;
using System.Text;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public class PackageArchiveComparerTests
{
    [Fact]
    public void Compare_DetectsAddedRemovedChangedFilesAndTfmFolders()
    {
        using var older = CreateNupkg(new Dictionary<string, string>
        {
            ["lib/netstandard2.0/Foo.dll"] = "old-dll",
            ["lib/netstandard2.0/Foo.xml"] = "docs",
            ["Foo.nuspec"] = Nuspec("1.0.0", "old desc")
        });
        using var newer = CreateNupkg(new Dictionary<string, string>
        {
            ["lib/net8.0/Foo.dll"] = "new-dll",
            ["lib/netstandard2.0/Foo.dll"] = "new-dll",
            ["Foo.nuspec"] = Nuspec("2.0.0", "new desc")
        });

        var result = PackageArchiveComparer.Compare(
            "Foo",
            "2.0.0",
            newer,
            "1.0.0",
            older,
            onlyOneVersionInFeed: false);

        Assert.Contains(result.FileChanges, static c => c.Kind == "added" && c.Path == "lib/net8.0/Foo.dll");
        Assert.Contains(result.FileChanges, static c => c.Kind == "removed" && c.Path == "lib/netstandard2.0/Foo.xml");
        Assert.Contains(result.FileChanges, static c => c.Kind == "changed" && c.Path == "lib/netstandard2.0/Foo.dll");
        Assert.Contains(result.AddedAssetFolders, static f => f == "lib/net8.0");
        Assert.DoesNotContain(result.RemovedAssetFolders, static f => f == "lib/netstandard2.0");
        Assert.Contains(result.NuspecChanges, static c => c.Field == "description" && c.OldValue == "old desc" && c.NewValue == "new desc");
        Assert.Contains(result.NuspecChanges, static c => c.Field == "version");
    }

    [Fact]
    public void Compare_SingleVersion_ListsFilesAndMessage()
    {
        using var pkg = CreateNupkg(new Dictionary<string, string>
        {
            ["lib/net8.0/Foo.dll"] = "dll",
            ["Foo.nuspec"] = Nuspec("1.0.0", "only")
        });

        var result = PackageArchiveComparer.Compare(
            "Foo",
            "1.0.0",
            pkg,
            compareVersion: null,
            comparePackage: null,
            onlyOneVersionInFeed: true);

        Assert.Equal("Only one version in this feed", result.Message);
        Assert.False(result.HasCompareVersion);
        Assert.Contains(result.CurrentFiles, static f => f.Path == "lib/net8.0/Foo.dll");
        Assert.Contains(result.CurrentAssetFolders, static f => f == "lib/net8.0");
        Assert.Empty(result.FileChanges);
    }

    private static MemoryStream CreateNupkg(IReadOnlyDictionary<string, string> files)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        ms.Position = 0;
        return ms;
    }

    private static string Nuspec(string version, string description) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>Foo</id>
            <version>{version}</version>
            <authors>AvantiPoint</authors>
            <description>{description}</description>
          </metadata>
        </package>
        """;
}
