using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Database.Sqlite;
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

public class CertificateDownloadApiTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;
    private readonly TestUrlGenerator _urlGenerator;

    public CertificateDownloadApiTests()
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
    public async Task GetCertificate_WithValidFingerprint_ReturnsCertificateFile()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/v3/certificates/{fingerprint}.crt");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/x-x509-ca-cert", response.Content.Headers.ContentType?.MediaType);
        
        var certificateBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(certificate.RawData, certificateBytes);
    }

    [Fact]
    public async Task GetCertificate_WithNonExistentFingerprint_Returns404()
    {
        // Arrange
        var client = CreateClient();
        var nonExistentFingerprint = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        // Act
        var response = await client.GetAsync($"/v3/certificates/{nonExistentFingerprint}.crt");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCertificate_WithMissingPublicCertificateBytes_Returns404()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        // Clear PublicCertificateBytes
        var saved = await _context.RepositorySigningCertificates.FirstAsync();
        saved.PublicCertificateBytes = null;
        await _context.SaveChangesAsync();

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/v3/certificates/{fingerprint}.crt");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCertificate_WithEmptyPublicCertificateBytes_Returns404()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        // Set PublicCertificateBytes to empty array
        var saved = await _context.RepositorySigningCertificates.FirstAsync();
        saved.PublicCertificateBytes = Array.Empty<byte>();
        await _context.SaveChangesAsync();

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/v3/certificates/{fingerprint}.crt");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCertificate_WithUppercaseFingerprint_NormalizesToLowercase()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate).ToUpperInvariant();
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/v3/certificates/{fingerprint}.crt");

        // Assert
        response.EnsureSuccessStatusCode();
        var certificateBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(certificate.RawData, certificateBytes);
    }

    [Fact]
    public async Task GetCertificate_WithEmptyFingerprint_Returns400()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/v3/certificates/.crt");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCertificate_ReturnsCorrectFileDownloadName()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/v3/certificates/{fingerprint}.crt");

        // Assert
        response.EnsureSuccessStatusCode();
        var contentDisposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(contentDisposition);
        Assert.Equal($"certificate-{fingerprint}.crt", contentDisposition.FileName);
    }

    [Fact]
    public async Task GetCertificate_ReturnsCorrectLastModifiedHeader()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/v3/certificates/{fingerprint}.crt");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(response.Content.Headers.LastModified);
        var saved = await _context.RepositorySigningCertificates.FirstAsync();
        Assert.Equal(saved.NotAfter, response.Content.Headers.LastModified.Value.DateTime);
    }

    [Fact]
    public async Task GetCertificate_IsAccessibleAnonymously()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var client = CreateClient();

        // Act
        var response = await client.GetAsync($"/v3/certificates/{fingerprint}.crt");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetCertificate_OnlyReturnsSha256Certificates()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(
            _context,
            NullLogger<RepositorySigningCertificateService>.Instance,
            TimeProvider.System,
            _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        // Create a certificate record with SHA-384 algorithm (should not be returned)
        var sha384Fingerprint = TestCertificateHelper.ComputeSha384Fingerprint(certificate);
        var sha384Record = new RepositorySigningCertificate
        {
            Fingerprint = sha384Fingerprint,
            HashAlgorithm = CertificateHashAlgorithm.Sha384,
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            NotBefore = certificate.NotBefore.ToUniversalTime(),
            NotAfter = certificate.NotAfter.ToUniversalTime(),
            FirstUsed = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            IsActive = true,
            PublicCertificateBytes = certificate.RawData
        };
        _context.RepositorySigningCertificates.Add(sha384Record);
        await _context.SaveChangesAsync();

        var sha256Fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var client = CreateClient();

        // Act - Try to get SHA-384 fingerprint (should return 404)
        var sha384Response = await client.GetAsync($"/v3/certificates/{sha384Fingerprint}.crt");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, sha384Response.StatusCode);

        // Act - Try to get SHA-256 fingerprint (should succeed)
        var sha256Response = await client.GetAsync($"/v3/certificates/{sha256Fingerprint}.crt");

        // Assert
        sha256Response.EnsureSuccessStatusCode();
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

