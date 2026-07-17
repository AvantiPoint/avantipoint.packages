using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Search.Tests;

public class ValidateSearchOptionsTests
{
    private readonly ValidateSearchOptions _validator = new();

    [Theory]
    [InlineData("Database")]
    [InlineData("Null")]
    [InlineData("AzureSearch")]
    [InlineData("OpenSearch")]
    [InlineData("Elasticsearch")]
    public void ValidSearchTypes_Succeed(string type)
    {
        var result = _validator.Validate(Options.DefaultName, new SearchOptions { Type = type });
        Assert.False(result.Failed);
    }

    [Fact]
    public void UnknownSearchType_Fails()
    {
        var result = _validator.Validate(Options.DefaultName, new SearchOptions { Type = "Solr" });
        Assert.True(result.Failed);
    }

    [Fact]
    public void NonPositiveUpstreamTimeout_Fails()
    {
        var result = _validator.Validate(
            Options.DefaultName,
            new SearchOptions { UpstreamSearchTimeout = TimeSpan.Zero });

        Assert.True(result.Failed);
    }

    [Fact]
    public void UnsupportedUpstreamTimeout_Fails()
    {
        var result = _validator.Validate(
            Options.DefaultName,
            new SearchOptions { UpstreamSearchTimeout = TimeSpan.FromDays(50) });

        Assert.True(result.Failed);
    }

    [Fact]
    public void UnknownMergeStrategy_Fails()
    {
        var result = _validator.Validate(
            Options.DefaultName,
            new SearchOptions { MergeStrategy = (FederatedSearchMergeStrategy)int.MaxValue });

        Assert.True(result.Failed);
    }
}
