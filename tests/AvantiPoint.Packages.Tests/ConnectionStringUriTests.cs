using AvantiPoint.Packages.Core;
using Xunit;

namespace AvantiPoint.Packages.Tests;

public class ConnectionStringUriTests
{
    [Fact]
    public void ParsesSchemeHostPortUserInfoAndPath()
    {
        Assert.True(ConnectionStringUri.TryParse("ftp://user:p%40ss@host.example:2121/some/path", out var uri));

        Assert.Equal("ftp", uri.Scheme);
        Assert.Equal("host.example", uri.Host);
        Assert.Equal(2121, uri.Port);
        Assert.Equal("user", uri.UserName);
        Assert.Equal("p@ss", uri.Password); // percent-decoded
        Assert.Equal("some/path", uri.Path);
    }

    [Fact]
    public void ParsesQueryParametersCaseInsensitivelyWithTypedAccessors()
    {
        Assert.True(ConnectionStringUri.TryParse(
            "s3://key:secret@bucket?region=us-west-2&forcePathStyle=true&maxConnections=8", out var uri));

        Assert.Equal("us-west-2", uri.GetString("REGION"));
        Assert.True(uri.GetBool("forcePathStyle"));
        Assert.Equal(8, uri.GetInt("maxConnections"));
        Assert.Null(uri.GetString("missing"));
        Assert.Null(uri.GetBool("missing"));
        Assert.Null(uri.GetInt("missing"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DefaultEndpointsProtocol=https;AccountName=acct;AccountKey=abc==")] // Azure-native, not a URI
    [InlineData("not a uri")]
    public void ReturnsFalseForNonUriValues(string value)
    {
        Assert.False(ConnectionStringUri.TryParse(value, out var uri));
        Assert.Null(uri);
    }

    [Fact]
    public void PathIsNullWhenAbsent()
    {
        Assert.True(ConnectionStringUri.TryParse("gs://my-bucket?prefix=packages", out var uri));

        Assert.Equal("my-bucket", uri.Host);
        Assert.Null(uri.Path);
        Assert.Equal("packages", uri.GetString("prefix"));
    }
}
