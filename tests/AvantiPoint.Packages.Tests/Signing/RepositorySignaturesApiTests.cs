using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages.Tests.Fixtures;
using IntegrationTestApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class RepositorySignaturesApiTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;
    private readonly TestUrlGenerator _urlGenerator;

    public RepositorySignaturesApiTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SqliteContext(options);
        _context.Database.EnsureCreated();
        _urlGenerator = new TestUrlGenerator();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private HttpClient CreateClient()
    {
        return new WebApplicationFactory<IntegrationTestApi.Program>()
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

                    // Add in-memory SQLite database using our connection
                    services.AddDbContext<SqliteContext>(options =>
                    {
                        options.UseSqlite(_connection);
                    });

                    // Replace IContext with our test context
                    var contextDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IContext));
                    if (contextDescriptor != null)
                    {
                        services.Remove(contextDescriptor);
                    }
                    services.AddScoped<IContext>(_ => _context);

                    // Replace RepositorySigningCertificateService with one using our context
                    var serviceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(RepositorySigningCertificateService));
                    if (serviceDescriptor != null)
                    {
                        services.Remove(serviceDescriptor);
                    }
                    services.AddScoped<RepositorySigningCertificateService>(_ =>
                        new RepositorySigningCertificateService(
                            _context,
                            NullLogger<RepositorySigningCertificateService>.Instance,
                            TimeProvider.System,
                            _urlGenerator));
                });

                builder.ConfigureServices(services =>
                {
                    // Ensure database is created
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IContext>();
                    db.Database.EnsureCreated();
                });
            })
            .CreateClient();
    }

    [Fact]
    public async Task GetRepositorySignatures_WithNoCertificates_ReturnsEmptyList()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RepositorySignaturesResponse>();

        Assert.NotNull(content);
        Assert.False(content.AllRepositorySigned);
        Assert.Empty(content.Certificates);
    }

    [Fact]
    public async Task GetRepositorySignatures_WithActiveCertificate_ReturnsCertificate()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RepositorySignaturesResponse>();

        Assert.NotNull(content);
        Assert.True(content.AllRepositorySigned);
        Assert.Single(content.Certificates);

        var cert = content.Certificates[0];
        Assert.Equal(certificate.Subject, cert.Subject);
        Assert.Equal(certificate.Issuer, cert.Issuer);
        Assert.Equal(TestCertificateHelper.ComputeSha256Fingerprint(certificate), cert.Fingerprints.Sha256);
        Assert.Null(cert.Fingerprints.Sha384);
        Assert.Null(cert.Fingerprints.Sha512);
        Assert.NotNull(cert.ContentUrl);
    }

    [Fact]
    public async Task GetRepositorySignatures_ExcludesInactiveCertificates()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var cert1 = TestCertificateHelper.CreateTestCertificate("CN=Active Certificate");
        var cert2 = TestCertificateHelper.CreateTestCertificate("CN=Inactive Certificate");
        await service.RecordCertificateUsageAsync(cert1);
        await service.RecordCertificateUsageAsync(cert2);

        var fingerprint2 = TestCertificateHelper.ComputeSha256Fingerprint(cert2);
        await service.DeactivateCertificateAsync(fingerprint2, CertificateHashAlgorithm.Sha256, "Test deactivation");

        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RepositorySignaturesResponse>();

        Assert.NotNull(content);
        Assert.Single(content.Certificates);
        Assert.Equal("CN=Active Certificate", content.Certificates[0].Subject);
    }

    [Fact]
    public async Task GetRepositorySignatures_ExcludesExpiredCertificatesNotRecentlyUsed()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var expiredCert = TestCertificateHelper.CreateExpiredCertificate();
        await service.RecordCertificateUsageAsync(expiredCert);

        // Set LastUsed to 100 days ago (outside 90-day grace period)
        var saved = await _context.RepositorySigningCertificates
            .FirstAsync(c => c.Subject == expiredCert.Subject);
        saved.LastUsed = DateTime.UtcNow.AddDays(-100);
        await _context.SaveChangesAsync();

        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RepositorySignaturesResponse>();

        Assert.NotNull(content);
        Assert.Empty(content.Certificates);
    }

    [Fact]
    public async Task GetRepositorySignatures_IncludesExpiredCertificatesRecentlyUsed()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var expiredCert = TestCertificateHelper.CreateExpiredCertificate();
        await service.RecordCertificateUsageAsync(expiredCert);

        // Set LastUsed to 30 days ago (within 90-day grace period)
        var saved = await _context.RepositorySigningCertificates
            .FirstAsync(c => c.Subject == expiredCert.Subject);
        saved.LastUsed = DateTime.UtcNow.AddDays(-30);
        await _context.SaveChangesAsync();

        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RepositorySignaturesResponse>();

        Assert.NotNull(content);
        Assert.Single(content.Certificates);
        Assert.Equal(expiredCert.Subject, content.Certificates[0].Subject);
    }

    [Fact]
    public async Task GetRepositorySignatures_ReturnsCorrectContentType()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetRepositorySignatures_IsAccessibleAnonymously()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetRepositorySignatures_WithMultipleCertificates_OrdersByFirstUsedDescending()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var cert1 = TestCertificateHelper.CreateTestCertificate("CN=First Certificate");
        var cert2 = TestCertificateHelper.CreateTestCertificate("CN=Second Certificate");
        var cert3 = TestCertificateHelper.CreateTestCertificate("CN=Third Certificate");

        await service.RecordCertificateUsageAsync(cert1);
        await Task.Delay(100); // Ensure time difference
        await service.RecordCertificateUsageAsync(cert2);
        await Task.Delay(100);
        await service.RecordCertificateUsageAsync(cert3);

        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RepositorySignaturesResponse>();

        Assert.NotNull(content);
        Assert.Equal(3, content.Certificates.Count);
        // Should be ordered by FirstUsed descending (newest first)
        Assert.Equal("CN=Third Certificate", content.Certificates[0].Subject);
        Assert.Equal("CN=Second Certificate", content.Certificates[1].Subject);
        Assert.Equal("CN=First Certificate", content.Certificates[2].Subject);
    }

    [Fact]
    public async Task GetRepositorySignatures_WithNullContentUrl_ExcludesContentUrlFromJson()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        // Clear ContentUrl
        var saved = await _context.RepositorySigningCertificates.FirstAsync();
        saved.ContentUrl = null;
        await _context.SaveChangesAsync();

        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/repository-signatures/index.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RepositorySignaturesResponse>();

        Assert.NotNull(content);
        Assert.Single(content.Certificates);
        Assert.Null(content.Certificates[0].ContentUrl);
    }

    private class TestUrlGenerator : IUrlGenerator
    {
        public string GetServiceIndexUrl() => "https://example.com/v3/index.json";
        public string GetPackageContentResourceUrl() => "https://example.com/v3/package";
        public string GetPackageMetadataResourceUrl() => "https://example.com/v3/registration";
        public string GetPackageMetadataResourceGzipSemVer1Url() => "https://example.com/v3/registration-gz-semver1";
        public string GetPackageMetadataResourceGzipSemVer2Url() => "https://example.com/v3/registration-gz-semver2";
        public string GetPackagePublishResourceUrl() => "https://example.com/v3/package";
        public string GetSymbolPublishResourceUrl() => "https://example.com/v3/symbol";
        public string GetSearchResourceUrl() => "https://example.com/v3/search";
        public string GetAutocompleteResourceUrl() => "https://example.com/v3/autocomplete";
        public string GetVulnerabilityIndexUrl() => "https://example.com/v3/vulnerabilities/index.json";
        public string GetPackageReadmeResourceUrl() => "https://example.com/v3/package/{lower_id}/{lower_version}/readme";
        public string GetRepositorySignaturesUrl() => "https://example.com/v3/repository-signatures/index.json";
        public string GetCertificateDownloadUrl(string fingerprint) => $"https://example.com/v3/certificates/{fingerprint.ToLowerInvariant()}.crt";
        public string GetRegistrationIndexUrl(string id) => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/index.json";
        public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper) => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/page.json";
        public string GetRegistrationLeafUrl(string id, NuGetVersion version) => $"https://example.com/v3/registration/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}.json";
        public string GetPackageVersionsUrl(string id) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/index.json";
        public string GetPackageDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.ToLowerInvariant()}.{version.ToNormalizedString().ToLowerInvariant()}.nupkg";
        public string GetPackageManifestDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.ToLowerInvariant()}.nuspec";
        public string GetPackageIconDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/icon";
        public string GetPackageLicenseDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/license";
        public string GetPackageReadmeDownloadUrl(string id, NuGetVersion version) => $"https://example.com/v3/package/{id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/readme";
    }
}

