using System.Net.Http.Json;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Protocol.Models;
using IntegrationTestApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;
using Xunit.Abstractions;

namespace AvantiPoint.Packages.Tests;

/// <summary>
/// Tests for NuGet 3.5.0 packageType filtering in search and autocomplete.
/// </summary>
public class PackageTypeSearchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<IntegrationTestApi.Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public PackageTypeSearchTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Create in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<IntegrationTestApi.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Remove any existing DbContext configurations
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<SqliteContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add in-memory SQLite database
                    services.AddDbContext<SqliteContext>(options =>
                    {
                        options.UseSqlite(_connection);
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Ensure database is created and seed test data
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IContext>();
                    db.Database.EnsureCreated();
                    
                    // Seed test packages with different package types
                    SeedTestPackages(db);
                });
            });

        _client = _factory.CreateClient();
    }

    private void SeedTestPackages(IContext context)
    {
        var packages = new List<Package>
        {
            // Package with DotnetTool type
            new Package
            {
                Id = "MyDotnetTool",
                Version = NuGetVersion.Parse("1.0.0"),
                Listed = true,
                IsPrerelease = false,
                Description = "A dotnet tool package",
                Authors = new[] { "Test Author" },
                Published = DateTime.UtcNow,
                PackageTypes = new List<PackageType>
                {
                    new PackageType { Name = "DotnetTool", Version = "1.0.0" }
                }
            },
            
            // Package with Dependency type (default)
            new Package
            {
                Id = "MyLibrary",
                Version = NuGetVersion.Parse("2.0.0"),
                Listed = true,
                IsPrerelease = false,
                Description = "A standard library package",
                Authors = new[] { "Test Author" },
                Published = DateTime.UtcNow,
                PackageTypes = new List<PackageType>
                {
                    new PackageType { Name = "Dependency", Version = "1.0.0" }
                }
            },
            
            // Package with Template type
            new Package
            {
                Id = "MyTemplate",
                Version = NuGetVersion.Parse("3.0.0"),
                Listed = true,
                IsPrerelease = false,
                Description = "A template package",
                Authors = new[] { "Test Author" },
                Published = DateTime.UtcNow,
                PackageTypes = new List<PackageType>
                {
                    new PackageType { Name = "Template", Version = "1.0.0" }
                }
            },
            
            // Package with custom type
            new Package
            {
                Id = "MyCustomPackage",
                Version = NuGetVersion.Parse("1.5.0"),
                Listed = true,
                IsPrerelease = false,
                Description = "A custom type package",
                Authors = new[] { "Test Author" },
                Published = DateTime.UtcNow,
                PackageTypes = new List<PackageType>
                {
                    new PackageType { Name = "CustomType", Version = "1.0.0" }
                }
            },
            
            // Package with multiple versions - only add one with DotnetTool type
            new Package
            {
                Id = "MyMultiVersionTool",
                Version = NuGetVersion.Parse("1.0.0"),
                Listed = true,
                IsPrerelease = false,
                Description = "Multi-version tool package v1",
                Authors = new[] { "Test Author" },
                Published = DateTime.UtcNow.AddDays(-10),
                PackageTypes = new List<PackageType>
                {
                    new PackageType { Name = "DotnetTool", Version = "1.0.0" }
                }
            },
            new Package
            {
                Id = "MyMultiVersionTool",
                Version = NuGetVersion.Parse("2.0.0"),
                Listed = true,
                IsPrerelease = false,
                Description = "Multi-version tool package v2",
                Authors = new[] { "Test Author" },
                Published = DateTime.UtcNow,
                PackageTypes = new List<PackageType>
                {
                    new PackageType { Name = "DotnetTool", Version = "1.0.0" }
                }
            },
            
            // Unlisted package should not appear in search
            new Package
            {
                Id = "UnlistedTool",
                Version = NuGetVersion.Parse("1.0.0"),
                Listed = false,
                IsPrerelease = false,
                Description = "An unlisted dotnet tool",
                Authors = new[] { "Test Author" },
                Published = DateTime.UtcNow,
                PackageTypes = new List<PackageType>
                {
                    new PackageType { Name = "DotnetTool", Version = "1.0.0" }
                }
            },
            
            // Prerelease package
            new Package
            {
                Id = "MyPrereleaseTemplate",
                Version = NuGetVersion.Parse("1.0.0-beta"),
                Listed = true,
                IsPrerelease = true,
                Description = "A prerelease template package",
                Authors = new[] { "Test Author" },
                Published = DateTime.UtcNow,
                PackageTypes = new List<PackageType>
                {
                    new PackageType { Name = "Template", Version = "1.0.0" }
                }
            }
        };

        context.Packages.AddRange(packages);
        context.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Search_WithoutPackageType_ReturnsAllListedPackages()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?q=&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        // Should return listed packages (6 listed packages: MyDotnetTool, MyLibrary, MyTemplate, MyCustomPackage, MyMultiVersionTool, MyPrereleaseTemplate excluded by default = 5)
        Assert.True(searchResponse.TotalHits >= 5, $"Should return at least 5 listed non-prerelease packages, got {searchResponse.TotalHits}");
        Assert.All(searchResponse.Data, pkg => Assert.NotNull(pkg.PackageTypes));
        
        _output.WriteLine($"Total hits without packageType filter: {searchResponse.TotalHits}");
        foreach (var pkg in searchResponse.Data)
        {
            _output.WriteLine($"  {pkg.PackageId} - Types: {string.Join(", ", pkg.PackageTypes.Select(t => t.Name))}");
        }
    }

    [Fact]
    public async Task Search_WithDotnetToolType_ReturnsOnlyDotnetTools()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?packageType=DotnetTool&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        Assert.Equal(2, searchResponse.TotalHits); // MyDotnetTool and MyMultiVersionTool (2 versions count as 1)
        Assert.All(searchResponse.Data, pkg =>
        {
            Assert.Contains(pkg.PackageTypes, pt => pt.Name == "DotnetTool");
        });
        
        _output.WriteLine($"DotnetTool packages found: {searchResponse.TotalHits}");
        foreach (var pkg in searchResponse.Data)
        {
            _output.WriteLine($"  {pkg.PackageId} v{pkg.Version}");
        }
    }

    [Fact]
    public async Task Search_WithTemplateType_ReturnsOnlyTemplates()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?packageType=Template&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        Assert.Equal(1, searchResponse.TotalHits); // Only MyTemplate (prerelease excluded by default)
        Assert.All(searchResponse.Data, pkg =>
        {
            Assert.Contains(pkg.PackageTypes, pt => pt.Name == "Template");
        });
        
        _output.WriteLine($"Template packages found: {searchResponse.TotalHits}");
    }

    [Fact]
    public async Task Search_WithTemplateTypeAndPrerelease_ReturnsStableAndPrereleaseTemplates()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?packageType=Template&prerelease=true&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        Assert.Equal(2, searchResponse.TotalHits); // MyTemplate and MyPrereleaseTemplate
        Assert.All(searchResponse.Data, pkg =>
        {
            Assert.Contains(pkg.PackageTypes, pt => pt.Name == "Template");
        });
        
        _output.WriteLine($"Template packages (including prerelease) found: {searchResponse.TotalHits}");
    }

    [Fact]
    public async Task Search_WithInvalidPackageType_ReturnsEmpty()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?packageType=NonExistentType&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        Assert.Equal(0, searchResponse.TotalHits);
        Assert.Empty(searchResponse.Data);
        
        _output.WriteLine("Invalid packageType correctly returned 0 results");
    }

    [Fact]
    public async Task Search_WithDependencyType_ReturnsOnlyDependencies()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?packageType=Dependency&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        Assert.True(searchResponse.TotalHits >= 1); // At least MyLibrary
        Assert.All(searchResponse.Data, pkg =>
        {
            Assert.Contains(pkg.PackageTypes, pt => pt.Name == "Dependency");
        });
        
        _output.WriteLine($"Dependency packages found: {searchResponse.TotalHits}");
    }

    [Fact]
    public async Task Search_WithCustomType_ReturnsOnlyCustomTypePackages()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?packageType=CustomType&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        Assert.Equal(1, searchResponse.TotalHits); // MyCustomPackage
        Assert.All(searchResponse.Data, pkg =>
        {
            Assert.Contains(pkg.PackageTypes, pt => pt.Name == "CustomType");
        });
        
        _output.WriteLine($"CustomType packages found: {searchResponse.TotalHits}");
    }

    [Fact]
    public async Task Search_UnlistedPackages_DoNotAppearWithPackageTypeFilter()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?packageType=DotnetTool&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        Assert.DoesNotContain(searchResponse.Data, pkg => pkg.PackageId == "UnlistedTool");
        
        _output.WriteLine("Unlisted packages correctly excluded from search results");
    }

    [Fact]
    public async Task Autocomplete_WithoutPackageType_ReturnsAllListedPackageIds()
    {
        // Act
        var response = await _client.GetAsync("/v3/autocomplete?q=My&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var autocompleteResponse = await response.Content.ReadFromJsonAsync<AutocompleteResponse>();
        
        Assert.NotNull(autocompleteResponse);
        // Should return at least 5 package IDs (6 total minus prerelease = 5 by default)
        Assert.True(autocompleteResponse.TotalHits >= 5, $"Should return at least 5 package IDs, got {autocompleteResponse.TotalHits}");
        Assert.DoesNotContain(autocompleteResponse.Data, id => id == "UnlistedTool");
        
        _output.WriteLine($"Total autocomplete results without packageType: {autocompleteResponse.TotalHits}");
        foreach (var id in autocompleteResponse.Data)
        {
            _output.WriteLine($"  {id}");
        }
    }

    [Fact]
    public async Task Autocomplete_WithDotnetToolType_ReturnsOnlyDotnetToolIds()
    {
        // Act
        var response = await _client.GetAsync("/v3/autocomplete?packageType=DotnetTool&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var autocompleteResponse = await response.Content.ReadFromJsonAsync<AutocompleteResponse>();
        
        Assert.NotNull(autocompleteResponse);
        Assert.Equal(2, autocompleteResponse.TotalHits); // MyDotnetTool and MyMultiVersionTool
        Assert.Contains(autocompleteResponse.Data, id => id == "MyDotnetTool");
        Assert.Contains(autocompleteResponse.Data, id => id == "MyMultiVersionTool");
        Assert.DoesNotContain(autocompleteResponse.Data, id => id == "MyLibrary");
        Assert.DoesNotContain(autocompleteResponse.Data, id => id == "MyTemplate");
        
        _output.WriteLine($"DotnetTool autocomplete results: {autocompleteResponse.TotalHits}");
    }

    [Fact]
    public async Task Autocomplete_WithTemplateType_ReturnsOnlyTemplateIds()
    {
        // Act
        var response = await _client.GetAsync("/v3/autocomplete?packageType=Template&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var autocompleteResponse = await response.Content.ReadFromJsonAsync<AutocompleteResponse>();
        
        Assert.NotNull(autocompleteResponse);
        Assert.Equal(1, autocompleteResponse.TotalHits); // Only MyTemplate (prerelease excluded)
        Assert.Contains(autocompleteResponse.Data, id => id == "MyTemplate");
        
        _output.WriteLine($"Template autocomplete results: {autocompleteResponse.TotalHits}");
    }

    [Fact]
    public async Task Autocomplete_WithTemplateTypeAndPrerelease_ReturnsStableAndPrereleaseIds()
    {
        // Act
        var response = await _client.GetAsync("/v3/autocomplete?packageType=Template&prerelease=true&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var autocompleteResponse = await response.Content.ReadFromJsonAsync<AutocompleteResponse>();
        
        Assert.NotNull(autocompleteResponse);
        Assert.Equal(2, autocompleteResponse.TotalHits); // MyTemplate and MyPrereleaseTemplate
        Assert.Contains(autocompleteResponse.Data, id => id == "MyTemplate");
        Assert.Contains(autocompleteResponse.Data, id => id == "MyPrereleaseTemplate");
        
        _output.WriteLine($"Template autocomplete results (with prerelease): {autocompleteResponse.TotalHits}");
    }

    [Fact]
    public async Task Autocomplete_WithInvalidPackageType_ReturnsEmpty()
    {
        // Act
        var response = await _client.GetAsync("/v3/autocomplete?packageType=NonExistentType&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var autocompleteResponse = await response.Content.ReadFromJsonAsync<AutocompleteResponse>();
        
        Assert.NotNull(autocompleteResponse);
        Assert.Equal(0, autocompleteResponse.TotalHits);
        Assert.Empty(autocompleteResponse.Data);
        
        _output.WriteLine("Invalid packageType correctly returned 0 autocomplete results");
    }

    [Fact]
    public async Task Autocomplete_UnlistedPackages_DoNotAppearWithPackageTypeFilter()
    {
        // Act
        var response = await _client.GetAsync("/v3/autocomplete?packageType=DotnetTool&take=100");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var autocompleteResponse = await response.Content.ReadFromJsonAsync<AutocompleteResponse>();
        
        Assert.NotNull(autocompleteResponse);
        Assert.DoesNotContain(autocompleteResponse.Data, id => id == "UnlistedTool");
        
        _output.WriteLine("Unlisted packages correctly excluded from autocomplete results");
    }

    [Fact]
    public async Task Search_PackageTypesField_IsPresentInResults()
    {
        // Act
        var response = await _client.GetAsync("/v3/search?q=MyDotnetTool");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var searchResponse = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResponse);
        Assert.NotEmpty(searchResponse.Data);
        
        var package = searchResponse.Data.First(p => p.PackageId == "MyDotnetTool");
        Assert.NotNull(package.PackageTypes);
        Assert.NotEmpty(package.PackageTypes);
        Assert.Equal("DotnetTool", package.PackageTypes.First().Name);
        
        _output.WriteLine($"PackageTypes field verified for {package.PackageId}:");
        foreach (var type in package.PackageTypes)
        {
            _output.WriteLine($"  - {type.Name}");
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
