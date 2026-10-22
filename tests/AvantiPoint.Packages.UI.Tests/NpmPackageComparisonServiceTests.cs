using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Npm;
using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NpmPackageComparisonServiceTests(OpenFeedFactory factory) : IClassFixture<OpenFeedFactory>
{
    [Fact]
    public async Task CompareAsync_ResolvesExactScopedVersionsUsingOnlyLocallyStoredTarballs()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var feedId = scope.ServiceProvider.GetRequiredService<IFeedRegistry>().Feed.FeedId;
        var packageName = $"@comparison/widget-{Guid.NewGuid():N}";
        var package = new NpmPackage
        {
            FeedId = feedId,
            Name = packageName,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
        };
        package.Versions.Add(new NpmVersion
        {
            FeedId = feedId,
            Package = package,
            Version = "1.0.0-beta.1",
            Published = DateTime.UtcNow.AddDays(-2),
            TarballPath = $"{packageName.Replace("/", "%2f", StringComparison.Ordinal)}/-/widget-1.0.0-beta.1.tgz",
            Shasum = "old",
            Origin = PackageOrigin.Published,
            PackumentJson = $$"""{"name":"{{packageName}}","version":"1.0.0-beta.1","type":"commonjs"}""",
        });
        package.Versions.Add(new NpmVersion
        {
            FeedId = feedId,
            Package = package,
            Version = "2.0.0+build.7",
            Published = DateTime.UtcNow.AddDays(-1),
            TarballPath = $"{packageName.Replace("/", "%2f", StringComparison.Ordinal)}/-/widget-2.0.0+build.7.tgz",
            Shasum = "new",
            Origin = PackageOrigin.Published,
            PackumentJson = $$"""{"name":"{{packageName}}","version":"2.0.0+build.7","type":"module"}""",
        });
        context.NpmPackages.Add(package);
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var blobStore = scope.ServiceProvider
            .GetRequiredService<IStorageBackendFactory>()
            .CreatePathStore("npm/");
        foreach (var version in package.Versions)
        {
            await using var tarball = CreateTarball(
                $"export const version = '{version.Version}';\n");
            await blobStore.PutAsync(
                version.TarballPath,
                tarball,
                Xunit.TestContext.Current.CancellationToken);
        }

        var service = scope.ServiceProvider.GetRequiredService<INpmPackageComparisonService>();

        var result = await service.CompareAsync(
            packageName,
            "1.0.0-beta.1",
            "2.0.0+build.7",
            "index.js",
            cancellationToken: Xunit.TestContext.Current.CancellationToken);
        var inexact = await service.CompareAsync(
            packageName,
            "1.0.0-BETA.1",
            "2.0.0+build.7",
            cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(packageName, result.PackageName);
        Assert.Equal("1.0.0-beta.1", result.BaseVersion);
        Assert.Equal("2.0.0+build.7", result.TargetVersion);
        Assert.Contains(result.MetadataChanges, change =>
            change.Field == "type" && change.BaseValue == "commonjs" && change.TargetValue == "module");
        Assert.Equal("available", result.ArchiveState);
        Assert.Equal("index.js", result.SelectedFile);
        Assert.Contains(result.SelectedFileDiff, line =>
            line.Kind == "del" && line.Text.Contains("1.0.0-beta.1", StringComparison.Ordinal));
        Assert.Contains(result.SelectedFileDiff, line =>
            line.Kind == "add" && line.Text.Contains("2.0.0+build.7", StringComparison.Ordinal));
        Assert.Null(inexact);
    }

    private static MemoryStream CreateTarball(string indexContent)
    {
        var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "package/index.js")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes(indexContent), writable: false),
                ModificationTime = DateTimeOffset.UnixEpoch,
            };
            writer.WriteEntry(entry);
        }

        output.Position = 0;
        return output;
    }
}
