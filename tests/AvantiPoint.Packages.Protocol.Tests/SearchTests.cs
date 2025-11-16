using AvantiPoint.Packages.Protocol.Models;
using NuGet.Versioning;
using Xunit.Abstractions;

namespace AvantiPoint.Packages.Protocol.Tests;

public class SearchTests(ITestOutputHelper output)
{
    private const string FeedUrl = "https://api.nuget.org/v3/index.json";
    private const string PackageId = "newtonsoft.json"; // lower-case for Exists endpoints
    private const string ExpectedCanonicalPackageId = "Newtonsoft.Json"; // expected casing returned by search/autocomplete
    private const string VersionString = "12.0.1";

    [Fact]
    public async Task Exists()
    {
        var client = new NuGetClient(FeedUrl);

        var existsId = await client.ExistsAsync(PackageId);
        output.WriteLine($"Exists({PackageId}) => {existsId}");
        Assert.True(existsId, $"Package '{PackageId}' should exist.");

        var packageVersion = NuGetVersion.Parse(VersionString);
        var existsVersion = await client.ExistsAsync(PackageId, packageVersion);
        output.WriteLine($"Exists({PackageId}, {packageVersion}) => {existsVersion}");
        Assert.True(existsVersion, $"Package '{PackageId}' version '{packageVersion}' should exist.");
    }

    [Fact]
    public async Task Search()
    {
        var client = new NuGetClient(FeedUrl);
        var results = await client.SearchAsync("json");

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        output.WriteLine($"Search returned {results.Count} results for query 'json'.");

        // Basic field assertions
        Assert.All(results, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.PackageId), "PackageId should be populated.");
            Assert.False(string.IsNullOrWhiteSpace(r.Version), "Version should be populated.");
            Assert.True(r.TotalDownloads >= 0, "TotalDownloads should be non-negative.");
            Assert.NotNull(r.Versions);
            Assert.True(r.Versions.Count >= 1, "Versions list should have at least one item.");
        });

        // Ensure the expected package appears in the results.
        Assert.Contains(results, r => string.Equals(r.PackageId, ExpectedCanonicalPackageId, StringComparison.Ordinal));

        var index = 1;
        foreach (SearchResult result in results)
        {
            output.WriteLine($"Result #{index}");
            output.WriteLine($"Package id: {result.PackageId}");
            output.WriteLine($"Package version: {result.Version}");
            output.WriteLine($"Package downloads: {result.TotalDownloads}");
            output.WriteLine($"Package versions: {result.Versions.Count}");
            output.WriteLine(string.Empty);
            index++;
        }
    }

    [Fact]
    public async Task Autocomplete()
    {
        var client = new NuGetClient(FeedUrl);
        var packageIds = await client.AutocompleteAsync("Newt");

        Assert.NotNull(packageIds);
        Assert.NotEmpty(packageIds);
        output.WriteLine($"Autocomplete returned {packageIds.Count} ids for prefix 'Newt'.");

        // Ensure the expected package id is suggested.
        Assert.Contains(packageIds, id => string.Equals(id, ExpectedCanonicalPackageId, StringComparison.Ordinal));

        foreach (var id in packageIds)
        {
            output.WriteLine($"Found package ID '{id}'");
        }
    }
}
