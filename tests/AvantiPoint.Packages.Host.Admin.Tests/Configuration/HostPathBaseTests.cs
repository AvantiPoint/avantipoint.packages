using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Configuration;

namespace AvantiPoint.Packages.Host.Admin.Tests.Configuration;

public sealed class HostPathBaseTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("/", "")]
    [InlineData("/packages/", "/packages")]
    public void Resolve_NormalizesConfiguredPath(string? configured, string expected)
    {
        var values = new Dictionary<string, string?>
        {
            [HostPathBase.ConfigurationKey] = configured,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var result = HostPathBase.Resolve(configuration);

        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("packages")]
    [InlineData("/packages?tenant=one")]
    [InlineData("/packages#fragment")]
    public void Resolve_RejectsValuesThatAreNotAPathBase(string configured)
    {
        var values = new Dictionary<string, string?>
        {
            [HostPathBase.ConfigurationKey] = configured,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.Throws<InvalidOperationException>(() => HostPathBase.Resolve(configuration));
    }
}
