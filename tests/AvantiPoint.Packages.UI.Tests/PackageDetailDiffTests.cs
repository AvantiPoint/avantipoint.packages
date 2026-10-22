using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.Configuration;
using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Tests;

public class PackageDetailDiffTests
{
    [Fact]
    public void RendersDiffVersionsFrameworksTabsAndCompareLinks()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddHttpClient();
        ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        ctx.Services.AddSingleton<IPackageDiffService>(new StubDiffService());

        var package = new PackageInfoCollection("2.0.0")
        {
            Versions =
            [
                new PackageInfo
                {
                    PackageId = "Foo",
                    Version = NuGetVersion.Parse("2.0.0"),
                    Description = "Current",
                    Authors = ["AvantiPoint"],
                    Tags = ["test"],
                    TargetFrameworks = ["net8.0", "netstandard2.0"],
                    Dependencies = new Dictionary<string, IEnumerable<PackageDependencyInfo>>(),
                    Published = DateTimeOffset.UtcNow,
                    IsListed = true
                },
                new PackageInfo
                {
                    PackageId = "Foo",
                    Version = NuGetVersion.Parse("1.0.0"),
                    Description = "Previous",
                    Authors = ["AvantiPoint"],
                    Tags = ["test"],
                    TargetFrameworks = ["netstandard2.0"],
                    Dependencies = new Dictionary<string, IEnumerable<PackageDependencyInfo>>(),
                    Published = DateTimeOffset.UtcNow.AddDays(-1),
                    IsListed = true
                }
            ]
        };

        var cut = ctx.Render<PackageDetail>(parameters => parameters
            .Add(p => p.Package, package)
            .Add(p => p.Compare, "1.0.0")
            .Add(p => p.Tab, "diff"));

        var markup = cut.Markup;
        Assert.Contains("Diff", markup, StringComparison.Ordinal);
        Assert.Contains("Versions", markup, StringComparison.Ordinal);
        Assert.Contains("Frameworks", markup, StringComparison.Ordinal);
        Assert.Contains(">Files<", markup, StringComparison.Ordinal);
        Assert.Contains("tab=diff", markup, StringComparison.Ordinal);
        Assert.Contains("compare=1.0.0", markup, StringComparison.Ordinal);
        Assert.Contains("lib/net8.0/Foo.dll/Foo.Bar.cs", markup, StringComparison.Ordinal);
        Assert.Contains("void NewMethod();", markup, StringComparison.Ordinal);
        Assert.Contains("pkg-diff-line-add", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Public API", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Package files", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy Copy", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Listed Listed", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick=\"@(() => SetContentTab", markup, StringComparison.Ordinal);
    }

    private sealed class StubDiffService : IPackageDiffService
    {
        public Task<PackageDiffResult> GetDiffAsync(
            string packageId,
            string currentVersion,
            string? compareVersion,
            bool onlyOneVersionInFeed,
            string? filePath = null,
            string? view = null,
            string? tfm = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageDiffResult
            {
                PackageId = packageId,
                CurrentVersion = currentVersion,
                CompareVersion = compareVersion,
                SelectedFile = "lib/net8.0/Foo.dll/Foo.Bar.cs",
                TextSource = "Decompiled",
                View = "unified",
                RightText = "public interface IBar { void NewMethod(); }",
                LeftText = "public interface IBar { }",
                Lines =
                [
                    new DiffLine { Kind = "eq", Text = "public interface IBar", LeftNumber = 1, RightNumber = 1 },
                    new DiffLine { Kind = "add", Text = "    void NewMethod();", RightNumber = 2 }
                ],
                Tree =
                [
                    new PackageDiffTreeNode
                    {
                        Path = "Foo.nuspec",
                        Name = "Foo.nuspec",
                        Kind = "file",
                        Change = "changed"
                    },
                    new PackageDiffTreeNode
                    {
                        Path = "lib/net8.0/Foo.dll",
                        Name = "lib/net8.0/Foo.dll",
                        Kind = "assembly",
                        Change = "changed",
                        Children =
                        [
                            new PackageDiffTreeNode
                            {
                                Path = "lib/net8.0/Foo.dll/Foo.Bar.cs",
                                Name = "Foo.Bar.cs",
                                Kind = "type",
                                Change = "changed"
                            }
                        ]
                    }
                ],
                CurrentAssetFolders = ["lib/net8.0"]
            });
        }
    }
}
