using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AvantiPoint.Packages.UI.Components.Npm;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Components;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NpmPackageComparisonBuilderTests
{
    [Fact]
    public async Task CompareAsync_ReportsStructuredMetadataAndStoredFileChanges()
    {
        await using var previous = CreateTarball(new Dictionary<string, byte[]>
        {
            ["package/package.json"] = Utf8("{\"name\":\"@example/widget\",\"version\":\"1.0.0\"}\n"),
            ["package/src/index.js"] = Utf8("export const value = 'old';\n"),
            ["package/removed.js"] = Utf8("export default true;\n"),
        });
        await using var current = CreateTarball(new Dictionary<string, byte[]>
        {
            ["package/package.json"] = Utf8("{\"name\":\"@example/widget\",\"version\":\"2.0.0\"}\n"),
            ["package/src/index.js"] = Utf8("export const value = 'new';\n"),
            ["package/assets/icon.bin"] = [0, 1, 2, 3],
        });

        var result = await NpmPackageComparisonBuilder.CompareAsync(
            "@example/widget",
            "1.0.0",
            """{"name":"@example/widget","version":"1.0.0","dependencies":{"react":"^18"},"readme":"ignored old"}""",
            previous,
            "2.0.0",
            """{"name":"@example/widget","version":"2.0.0","dependencies":{"react":"^19"},"type":"module","readme":"ignored new"}""",
            current,
            "src/index.js",
            Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("available", result.ArchiveState);
        Assert.Contains(result.MetadataChanges, change =>
            change.Field == "dependencies.react"
            && change.BaseValue == "^18"
            && change.TargetValue == "^19");
        Assert.Contains(result.MetadataChanges, change => change.Field == "type" && change.ChangeKind == "added");
        Assert.DoesNotContain(result.MetadataChanges, change => change.Field.Equals("readme", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, result.FileChanges.Count);
        Assert.Contains(result.FileChanges, change => change.Path == "src/index.js" && change.ChangeKind == "changed");
        Assert.Contains(result.FileChanges, change => change.Path == "removed.js" && change.ChangeKind == "removed");
        Assert.Contains(result.FileChanges, change => change.Path == "assets/icon.bin" && !change.HasTextPreview);
        Assert.DoesNotContain(result.FileChanges, change => change.Path.StartsWith("package/", StringComparison.Ordinal));
        Assert.Equal("src/index.js", result.SelectedFile);
        Assert.Contains(result.SelectedFileDiff, line => line.Kind == "del" && line.Text.Contains("old", StringComparison.Ordinal));
        Assert.Contains(result.SelectedFileDiff, line => line.Kind == "add" && line.Text.Contains("new", StringComparison.Ordinal));
        Assert.Contains(result.FileTree, node => node.Name == "src" && node.IsDirectory);
    }

    [Fact]
    public async Task CompareAsync_MissingTarballsKeepsMetadataDiffAndExplainsNoUpstreamFetch()
    {
        var result = await NpmPackageComparisonBuilder.CompareAsync(
            "example",
            "1.0.0",
            """{"name":"example","version":"1.0.0"}""",
            null,
            "2.0.0",
            """{"name":"example","version":"2.0.0"}""",
            null,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("unavailable", result.ArchiveState);
        Assert.Contains("Neither tarball is stored", result.ArchiveMessage, StringComparison.Ordinal);
        Assert.Contains("does not download", result.ArchiveMessage, StringComparison.Ordinal);
        Assert.Contains(result.MetadataChanges, change => change.Field == "version");
        Assert.Empty(result.FileChanges);
    }

    [Fact]
    public async Task CompareAsync_RejectsTraversalPathsWithoutExtractingContent()
    {
        await using var unsafeTarball = CreateTarball(new Dictionary<string, byte[]>
        {
            ["package/../../escape.js"] = Utf8("throw new Error('never run');\n"),
        });
        await using var validTarball = CreateTarball(new Dictionary<string, byte[]>
        {
            ["package/index.js"] = Utf8("export {};\n"),
        });

        var result = await NpmPackageComparisonBuilder.CompareAsync(
            "example",
            "1.0.0",
            """{"version":"1.0.0"}""",
            unsafeTarball,
            "2.0.0",
            """{"version":"2.0.0"}""",
            validTarball,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("unavailable", result.ArchiveState);
        Assert.Contains("unsafe or invalid path", result.ArchiveMessage, StringComparison.Ordinal);
        Assert.Empty(result.FileChanges);
    }

    [Fact]
    public async Task Component_EncodesUntrustedMetadataPathsAndTextDiffLines()
    {
        await using var previous = CreateTarball(new Dictionary<string, byte[]>
        {
            ["package/<img src=x onerror=alert(1)>.js"] = Utf8("export const html = '<b>old</b>';\n"),
        });
        await using var current = CreateTarball(new Dictionary<string, byte[]>
        {
            ["package/<img src=x onerror=alert(1)>.js"] = Utf8("<script>alert('x')</script>\n"),
        });
        var result = await NpmPackageComparisonBuilder.CompareAsync(
            "example",
            "1.0.0",
            """{"description":"<img src=x onerror=alert(2)>"}""",
            previous,
            "2.0.0",
            """{"description":"<script>alert(2)</script>"}""",
            current,
            "<img src=x onerror=alert(1)>.js",
            Xunit.TestContext.Current.CancellationToken);
        using var context = new BunitContext();

        var component = context.Render<NpmPackageComparison>(parameters => parameters
            .Add(parameter => parameter.Versions,
            [
                new NpmVersionListItem("2.0.0", DateTime.UtcNow, "new"),
                new NpmVersionListItem("1.0.0", DateTime.UtcNow.AddDays(-1), "old"),
            ])
            .Add(parameter => parameter.TargetVersion, "2.0.0")
            .Add(parameter => parameter.CompareVersion, "1.0.0")
            .Add(parameter => parameter.Comparison, result));

        Assert.Contains("&lt;script&gt;alert(2)&lt;/script&gt;", component.Markup, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert('x')&lt;/script&gt;", component.Markup, StringComparison.Ordinal);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;.js", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindAll("script"));
        Assert.Empty(component.FindAll("img"));
    }

    [Fact]
    public async Task Component_PreservesPathBaseAndExactVersionPairInFileUrl()
    {
        await using var previous = CreateTarball(new Dictionary<string, byte[]>
        {
            ["package/src/index.ts"] = Utf8("export const version = 1;\n"),
        });
        await using var current = CreateTarball(new Dictionary<string, byte[]>
        {
            ["package/src/index.ts"] = Utf8("export const version = 2;\n"),
        });
        var result = await NpmPackageComparisonBuilder.CompareAsync(
            "@example/widget",
            "1.0.0-beta.1",
            """{"version":"1.0.0-beta.1"}""",
            previous,
            "2.0.0+build.7",
            """{"version":"2.0.0+build.7"}""",
            current,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);
        using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/tenant/packages/browse/npm/packages/%40example/widget?version=2.0.0%2Bbuild.7&compare=1.0.0-beta.1");
        var component = context.Render<NpmPackageComparison>(parameters => parameters
            .Add(parameter => parameter.Versions,
            [
                new NpmVersionListItem("2.0.0+build.7", DateTime.UtcNow, "new"),
                new NpmVersionListItem("1.0.0-beta.1", DateTime.UtcNow.AddDays(-1), "old"),
            ])
            .Add(parameter => parameter.TargetVersion, "2.0.0+build.7")
            .Add(parameter => parameter.CompareVersion, "1.0.0-beta.1")
            .Add(parameter => parameter.Comparison, result));

        component.Find("button[aria-label='Inspect src/index.ts']").Click();

        var destination = new Uri(navigation.Uri);
        Assert.Equal("/tenant/packages/browse/npm/packages/%40example/widget", destination.AbsolutePath);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(destination.Query);
        Assert.Equal("2.0.0+build.7", query["version"].ToString());
        Assert.Equal("1.0.0-beta.1", query["compare"].ToString());
        Assert.Equal("src/index.ts", query["file"].ToString());
    }

    private static MemoryStream CreateTarball(IReadOnlyDictionary<string, byte[]> files)
    {
        var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            foreach (var (path, bytes) in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(bytes, writable: false),
                    ModificationTime = DateTimeOffset.UnixEpoch,
                };
                writer.WriteEntry(entry);
            }
        }

        output.Position = 0;
        return output;
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
