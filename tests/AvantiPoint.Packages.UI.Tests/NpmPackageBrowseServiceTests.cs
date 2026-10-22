using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Npm;
using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NpmPackageBrowseServiceTests(OpenFeedFactory factory) : IClassFixture<OpenFeedFactory>
{
    [Fact]
    public async Task SearchAsync_MatchesDescriptionKeywordsAndMaintainers()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var feedId = scope.ServiceProvider.GetRequiredService<IFeedRegistry>().Feed.FeedId;
        var marker = $"discover-{Guid.NewGuid():N}";
        var descriptionMatch = $"description-{Guid.NewGuid():N}";
        var keywordMatch = $"keyword-{Guid.NewGuid():N}";
        var maintainerMatch = $"maintainer-{Guid.NewGuid():N}";

        context.NpmPackages.AddRange(
            CreateSearchPackage(feedId, descriptionMatch, description: $"Utilities for {marker} projects."),
            CreateSearchPackage(feedId, keywordMatch, keywords: ["tooling", marker]),
            CreateSearchPackage(feedId, maintainerMatch, maintainer: $"{marker} team"),
            CreateSearchPackage(feedId, $"unrelated-{Guid.NewGuid():N}", description: "A different package."));
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var service = scope.ServiceProvider.GetRequiredService<INpmPackageBrowseService>();
        var results = await service.SearchAsync(
            marker,
            skip: 0,
            take: 100,
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(results, package => package.Name == descriptionMatch);
        Assert.Contains(results, package => package.Name == keywordMatch);
        Assert.Contains(results, package => package.Name == maintainerMatch);
        Assert.DoesNotContain(results, package => package.Name.StartsWith("unrelated-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetPackageAsync_ProjectsRichMetadataFromLatestDistTag()
    {
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var feedId = scope.ServiceProvider.GetRequiredService<IFeedRegistry>().Feed.FeedId;
        var packageName = $"@avantipoint/widgets-{Guid.NewGuid():N}";
        var package = new NpmPackage
        {
            FeedId = feedId,
            Name = packageName,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
        };
        var stable = new NpmVersion
        {
            FeedId = feedId,
            Package = package,
            Version = "1.0.0",
            Published = DateTime.UtcNow.AddDays(-2),
            TarballPath = $"{packageName}/-/widgets-1.0.0.tgz",
            Shasum = "stable-sha",
            Origin = PackageOrigin.Published,
            PackumentJson = BuildPackument(packageName, "1.0.0", "Stable description", "# Stable readme"),
        };
        var prerelease = new NpmVersion
        {
            FeedId = feedId,
            Package = package,
            Version = "2.0.0-beta.1",
            Published = DateTime.UtcNow.AddDays(-1),
            TarballPath = $"{packageName}/-/widgets-2.0.0-beta.1.tgz",
            Shasum = "preview-sha",
            Origin = PackageOrigin.Published,
            PackumentJson = BuildPackument(packageName, "2.0.0-beta.1", "Preview description", "# Preview readme"),
        };
        package.Versions.Add(stable);
        package.Versions.Add(prerelease);
        package.DistTags.Add(new NpmDistTag
        {
            FeedId = feedId,
            Package = package,
            Tag = "latest",
            Version = stable.Version,
        });
        package.DistTags.Add(new NpmDistTag
        {
            FeedId = feedId,
            Package = package,
            Tag = "next",
            Version = prerelease.Version,
        });
        context.NpmPackages.Add(package);
        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        var service = scope.ServiceProvider.GetRequiredService<INpmPackageBrowseService>();
        var result = await service.GetPackageAsync(
            packageName,
            cancellationToken: Xunit.TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Stable description", result.Description);
        Assert.Equal("# Stable readme", result.Readme);
        Assert.Equal("README.md", result.ReadmeFileName);
        Assert.Equal("MIT", result.License);
        Assert.Equal("https://github.com/avantipoint/widgets", result.RepositoryUrl);
        Assert.Equal("packages/widgets", result.RepositoryDirectory);
        Assert.Equal("Package Team", Assert.Single(result.Maintainers!).Name);
        Assert.Equal("https://github.com/sponsors/avantipoint", Assert.Single(result.Funding!).Url);

        var stableResult = result.Versions.Single(version => version.Version == stable.Version);
        Assert.Equal("sha512-AbCdEf==", stableResult.Integrity);
        Assert.Equal($"{packageName.Split('/').Last()}-1.0.0.tgz", stableResult.TarballFileName);
        Assert.Equal(4096, stableResult.UnpackedSize);
        Assert.Equal(27, stableResult.FileCount);
        Assert.Equal("^4.1.5", stableResult.Dependencies!["kleur"]);
        Assert.Equal(">=18", stableResult.PeerDependencies!["react"]);
        Assert.Contains("react", stableResult.OptionalPeerDependencies!);
        Assert.Equal(">=20", stableResult.Engines!["node"]);
        Assert.Equal("./dist/index.d.ts", stableResult.TypesEntryPoint);
        Assert.Equal("publisher", stableResult.Publisher?.Name);
        Assert.Equal("11.4.2", stableResult.PublishingNpmVersion);
        Assert.Equal("22.14.0", stableResult.PublishingNodeVersion);
        Assert.Equal("0123456789abcdef", stableResult.GitHead);
        Assert.Equal(2, stableResult.RegistrySignatureCount);
        Assert.Equal("https://registry.example/-/npm/v1/attestations/widgets@1.0.0", stableResult.Provenance?.Url);
        Assert.Equal("https://slsa.dev/provenance/v1", stableResult.Provenance?.PredicateType);
        Assert.NotNull(stableResult.Metadata);
        Assert.Equal("Stable description", stableResult.Metadata.Description);
        Assert.Equal("# Stable readme", stableResult.Metadata.Readme);
        Assert.Equal("MIT", stableResult.Metadata.License);
        Assert.Equal("https://github.com/avantipoint/widgets", stableResult.Metadata.RepositoryUrl);

        var prereleaseResult = result.Versions.Single(version => version.Version == prerelease.Version);
        Assert.Null(prereleaseResult.Metadata);
        Assert.Null(prereleaseResult.Dependencies);

        var prereleaseDetail = await service.GetPackageAsync(
            packageName,
            prerelease.Version,
            Xunit.TestContext.Current.CancellationToken);
        Assert.NotNull(prereleaseDetail);
        prereleaseResult = prereleaseDetail.Versions.Single(version => version.Version == prerelease.Version);
        Assert.NotNull(prereleaseResult.Metadata);
        Assert.Equal("Preview description", prereleaseResult.Metadata.Description);
        Assert.Equal("# Preview readme", prereleaseResult.Metadata.Readme);
        Assert.Equal("^4.1.5", prereleaseResult.Dependencies!["kleur"]);
    }

    private static string BuildPackument(
        string packageName,
        string version,
        string description,
        string readme)
    {
        JsonObject metadata = new()
        {
            ["name"] = packageName,
            ["version"] = version,
            ["description"] = description,
            ["keywords"] = new JsonArray("widgets", "ui"),
            ["readme"] = readme,
            ["readmeFilename"] = "README.md",
            ["license"] = "MIT",
            ["homepage"] = "https://example.test/widgets",
            ["repository"] = new JsonObject
            {
                ["type"] = "git",
                ["url"] = "git+https://github.com/avantipoint/widgets",
                ["directory"] = "packages/widgets",
            },
            ["bugs"] = new JsonObject
            {
                ["url"] = "https://github.com/avantipoint/widgets/issues",
                ["email"] = "support@example.test",
            },
            ["author"] = new JsonObject { ["name"] = "AvantiPoint" },
            ["maintainers"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "Package Team",
                    ["email"] = "packages@example.test",
                }),
            ["funding"] = new JsonObject
            {
                ["type"] = "GitHub Sponsors",
                ["url"] = "https://github.com/sponsors/avantipoint",
            },
            ["dependencies"] = new JsonObject { ["kleur"] = "^4.1.5" },
            ["devDependencies"] = new JsonObject { ["vitest"] = "^3.0.0" },
            ["peerDependencies"] = new JsonObject { ["react"] = ">=18" },
            ["peerDependenciesMeta"] = new JsonObject
            {
                ["react"] = new JsonObject { ["optional"] = true },
            },
            ["optionalDependencies"] = new JsonObject { ["fsevents"] = "^2.3.3" },
            ["engines"] = new JsonObject { ["node"] = ">=20" },
            ["packageManager"] = "npm@11.0.0",
            ["type"] = "module",
            ["types"] = "./dist/index.d.ts",
            ["_npmUser"] = new JsonObject
            {
                ["name"] = "publisher",
                ["email"] = "publisher@example.test",
            },
            ["_npmVersion"] = "11.4.2",
            ["_nodeVersion"] = "22.14.0",
            ["gitHead"] = "0123456789abcdef",
            ["dist"] = new JsonObject
            {
                ["tarball"] = $"https://registry.example/{packageName.Split('/').Last()}-{version}.tgz",
                ["integrity"] = "sha512-AbCdEf==",
                ["shasum"] = "upstream-sha",
                ["unpackedSize"] = 4096,
                ["fileCount"] = 27,
                ["signatures"] = new JsonArray(
                    new JsonObject { ["keyid"] = "key-a", ["sig"] = "signature-a" },
                    new JsonObject { ["keyid"] = "key-b", ["sig"] = "signature-b" }),
                ["attestations"] = new JsonObject
                {
                    ["url"] = "https://registry.example/-/npm/v1/attestations/widgets@1.0.0",
                    ["provenance"] = new JsonObject
                    {
                        ["predicateType"] = "https://slsa.dev/provenance/v1",
                    },
                },
            },
        };

        return metadata.ToJsonString();
    }

    private static NpmPackage CreateSearchPackage(
        string feedId,
        string packageName,
        string? description = null,
        IReadOnlyList<string>? keywords = null,
        string? maintainer = null)
    {
        const string version = "1.0.0";
        JsonObject metadata = new()
        {
            ["name"] = packageName,
            ["version"] = version,
            ["description"] = description,
            ["keywords"] = keywords is null
                ? new JsonArray()
                : new JsonArray(keywords.Select(keyword => JsonValue.Create(keyword)).ToArray()),
            ["maintainers"] = string.IsNullOrWhiteSpace(maintainer)
                ? new JsonArray()
                : new JsonArray(new JsonObject { ["name"] = maintainer }),
            ["dist"] = new JsonObject
            {
                ["tarball"] = $"https://registry.example/{packageName}-{version}.tgz",
                ["shasum"] = "search-sha",
            },
        };
        var package = new NpmPackage
        {
            FeedId = feedId,
            Name = packageName,
            CreatedAt = DateTime.UtcNow,
        };
        package.Versions.Add(new NpmVersion
        {
            FeedId = feedId,
            Package = package,
            Version = version,
            Published = DateTime.UtcNow,
            TarballPath = $"{packageName}/-/{packageName}-{version}.tgz",
            Shasum = "search-sha",
            Origin = PackageOrigin.Published,
            PackumentJson = metadata.ToJsonString(),
        });
        package.DistTags.Add(new NpmDistTag
        {
            FeedId = feedId,
            Package = package,
            Tag = "latest",
            Version = version,
        });
        return package;
    }
}
