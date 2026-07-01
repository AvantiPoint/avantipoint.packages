using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AvantiPoint.Packages.Tests;

public class DatabaseConnectionStringNameTests
{
    private static DatabaseOptions Resolve(IDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions();
        services.AddNuGetApiOptions<DatabaseOptions>(nameof(PackageFeedOptions.Database));

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    }

    [Fact]
    public void ResolvesConnectionStringFromName_WhenNoInlineConnectionString()
    {
        var options = Resolve(new Dictionary<string, string?>
        {
            ["Database:Type"] = "Sqlite",
            ["Database:ConnectionStringName"] = "Packages",
            ["ConnectionStrings:Packages"] = "Data Source=packages.db",
        });

        Assert.Equal("Data Source=packages.db", options.ConnectionString);
    }

    [Fact]
    public void InlineConnectionStringTakesPrecedenceOverName()
    {
        var options = Resolve(new Dictionary<string, string?>
        {
            ["Database:Type"] = "Sqlite",
            ["Database:ConnectionString"] = "Data Source=inline.db",
            ["Database:ConnectionStringName"] = "Packages",
            ["ConnectionStrings:Packages"] = "Data Source=named.db",
        });

        Assert.Equal("Data Source=inline.db", options.ConnectionString);
    }

    [Fact]
    public void ThrowsWhenNamedConnectionStringIsMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Resolve(new Dictionary<string, string?>
        {
            ["Database:Type"] = "Sqlite",
            ["Database:ConnectionStringName"] = "DoesNotExist",
        }));

        Assert.Contains("DoesNotExist", ex.Message);
    }

    [Fact]
    public void InlineConnectionString_StillWorks_WithoutName()
    {
        var options = Resolve(new Dictionary<string, string?>
        {
            ["Database:Type"] = "Sqlite",
            ["Database:ConnectionString"] = "Data Source=inline.db",
        });

        Assert.Equal("Data Source=inline.db", options.ConnectionString);
    }
}
