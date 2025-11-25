using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class RepositorySigningCertificateServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;
    private readonly TestUrlGenerator _urlGenerator;
    private static CancellationToken CurrentCancellationToken => TestContext.Current.CancellationToken;

    public RepositorySigningCertificateServiceTests()
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

    [Fact]
    public async Task RecordCertificateUsageAsync_WithNewCertificate_CreatesNewRecord()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");

        // Act
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);

        // Assert
        var saved = await _context.RepositorySigningCertificates.FirstOrDefaultAsync(cancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(TestCertificateHelper.ComputeSha256Fingerprint(certificate), saved.Fingerprint);
        Assert.Equal(CertificateHashAlgorithm.Sha256, saved.HashAlgorithm);
        Assert.Equal(certificate.Subject, saved.Subject);
        Assert.Equal(certificate.Issuer, saved.Issuer);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task RecordCertificateUsageAsync_WithExistingCertificate_UpdatesLastUsed()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");

        // First usage
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);
        var firstUsage = await _context.RepositorySigningCertificates.FirstAsync(cancellationToken);
        var originalFirstUsed = firstUsage.FirstUsed;
        var originalLastUsed = firstUsage.LastUsed;

        // Wait a moment to ensure time difference
        await Task.Delay(100, cancellationToken);

        // Act - Second usage
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);

        // Assert
        var updated = await _context.RepositorySigningCertificates.FirstAsync(cancellationToken);
        Assert.Equal(originalFirstUsed, updated.FirstUsed); // FirstUsed should not change
        Assert.True(updated.LastUsed > originalLastUsed); // LastUsed should be updated
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_ReturnsOnlyActiveCertificates()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);

        var cert1 = TestCertificateHelper.CreateTestCertificate("CN=Active Certificate 1");
        var cert2 = TestCertificateHelper.CreateTestCertificate("CN=Active Certificate 2");
        var cert3 = TestCertificateHelper.CreateTestCertificate("CN=Inactive Certificate");

        await service.RecordCertificateUsageAsync(cert1, cancellationToken);
        await service.RecordCertificateUsageAsync(cert2, cancellationToken);
        await service.RecordCertificateUsageAsync(cert3, cancellationToken);

        // Deactivate cert3
        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(cert3);
        await service.DeactivateCertificateAsync(fingerprint, CertificateHashAlgorithm.Sha256, "Test deactivation", cancellationToken);

        // Act
        var activeCertificates = await service.GetActiveCertificatesAsync(cancellationToken);

        // Assert
        Assert.Equal(2, activeCertificates.Count);
        Assert.DoesNotContain(activeCertificates, c => c.Subject == "CN=Inactive Certificate");
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_ExcludesExpiredCertificatesNotRecentlyUsed()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);

        // Create and record an expired certificate that was last used 100 days ago
        var expiredCert = TestCertificateHelper.CreateExpiredCertificate();
        await service.RecordCertificateUsageAsync(expiredCert, cancellationToken);

        var saved = await _context.RepositorySigningCertificates
            .FirstAsync(c => c.Subject == expiredCert.Subject, cancellationToken);
        saved.LastUsed = DateTime.UtcNow.AddDays(-100);
        await _context.SaveChangesAsync(cancellationToken);

        // Act
        var activeCertificates = await service.GetActiveCertificatesAsync(cancellationToken);

        // Assert
        Assert.Empty(activeCertificates);
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_IncludesExpiredCertificatesRecentlyUsed()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);

        // Create and record an expired certificate that was last used 30 days ago
        var expiredCert = TestCertificateHelper.CreateExpiredCertificate();
        await service.RecordCertificateUsageAsync(expiredCert, cancellationToken);

        var saved = await _context.RepositorySigningCertificates
            .FirstAsync(c => c.Subject == expiredCert.Subject, cancellationToken);
        saved.LastUsed = DateTime.UtcNow.AddDays(-30); // Within 90-day grace period
        await _context.SaveChangesAsync(cancellationToken);

        // Act
        var activeCertificates = await service.GetActiveCertificatesAsync(cancellationToken);

        // Assert
        Assert.Single(activeCertificates);
    }

    [Fact]
    public async Task DeactivateCertificateAsync_MarksCertificateAsInactive()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);

        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);

        // Act
        await service.DeactivateCertificateAsync(fingerprint, CertificateHashAlgorithm.Sha256, "Certificate compromised", cancellationToken);

        // Assert
        var deactivated = await _context.RepositorySigningCertificates.FirstAsync(cancellationToken);
        Assert.False(deactivated.IsActive);
        Assert.Equal("Certificate compromised", deactivated.Notes);
    }

    [Fact]
    public async Task DeactivateCertificateAsync_WithNonExistentCertificate_DoesNotThrow()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);

        // Act & Assert - Should not throw
        await service.DeactivateCertificateAsync("nonexistent-fingerprint", CertificateHashAlgorithm.Sha256, "test", cancellationToken);
    }

    [Fact]
    public async Task RecordCertificateUsageAsync_StoresFingerprintWithHashAlgorithm()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");

        // Act
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);

        // Assert
        var saved = await _context.RepositorySigningCertificates.FirstAsync(cancellationToken);
        Assert.NotNull(saved.Fingerprint);
        Assert.Equal(CertificateHashAlgorithm.Sha256, saved.HashAlgorithm);
        Assert.Equal(64, saved.Fingerprint.Length); // SHA-256: 32 bytes * 2 hex chars
    }

    [Fact]
    public async Task RecordCertificateUsageAsync_UpdatesPublicCertificateBytesWhenNull()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        
        // Create record without PublicCertificateBytes
        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var existingRecord = new RepositorySigningCertificate
        {
            Fingerprint = fingerprint,
            HashAlgorithm = CertificateHashAlgorithm.Sha256,
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            NotBefore = certificate.NotBefore.ToUniversalTime(),
            NotAfter = certificate.NotAfter.ToUniversalTime(),
            FirstUsed = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            IsActive = true,
            PublicCertificateBytes = null // Initially null
        };
        _context.RepositorySigningCertificates.Add(existingRecord);
        await _context.SaveChangesAsync(cancellationToken);

        // Act
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);

        // Assert
        var updated = await _context.RepositorySigningCertificates.FirstAsync(cancellationToken);
        Assert.NotNull(updated.PublicCertificateBytes);
        Assert.Equal(certificate.RawData, updated.PublicCertificateBytes);
    }

    [Fact]
    public async Task RecordCertificateUsageAsync_UpdatesContentUrlWhenNull()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        
        // Create record without ContentUrl
        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);
        var existingRecord = new RepositorySigningCertificate
        {
            Fingerprint = fingerprint,
            HashAlgorithm = CertificateHashAlgorithm.Sha256,
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            NotBefore = certificate.NotBefore.ToUniversalTime(),
            NotAfter = certificate.NotAfter.ToUniversalTime(),
            FirstUsed = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            IsActive = true,
            PublicCertificateBytes = null, // Initially null to test ContentUrl update
            ContentUrl = null // Initially null
        };
        _context.RepositorySigningCertificates.Add(existingRecord);
        await _context.SaveChangesAsync(cancellationToken);

        // Act
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);

        // Assert
        var updated = await _context.RepositorySigningCertificates.FirstAsync(cancellationToken);
        Assert.NotNull(updated.ContentUrl);
        Assert.Equal(_urlGenerator.GetCertificateDownloadUrl(fingerprint), updated.ContentUrl);
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_OrdersByFirstUsedDescending()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var cert1 = TestCertificateHelper.CreateTestCertificate("CN=First Certificate");
        var cert2 = TestCertificateHelper.CreateTestCertificate("CN=Second Certificate");
        var cert3 = TestCertificateHelper.CreateTestCertificate("CN=Third Certificate");

        await service.RecordCertificateUsageAsync(cert1, cancellationToken);
        await Task.Delay(100, cancellationToken); // Ensure time difference
        await service.RecordCertificateUsageAsync(cert2, cancellationToken);
        await Task.Delay(100, cancellationToken);
        await service.RecordCertificateUsageAsync(cert3, cancellationToken);

        // Act
        var certificates = await service.GetActiveCertificatesAsync(cancellationToken);

        // Assert
        Assert.Equal(3, certificates.Count);
        // Should be ordered by FirstUsed descending (newest first)
        Assert.Equal("CN=Third Certificate", certificates[0].Subject);
        Assert.Equal("CN=Second Certificate", certificates[1].Subject);
        Assert.Equal("CN=First Certificate", certificates[2].Subject);
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_WithMultipleCertificates_ReturnsAllActive()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var cert1 = TestCertificateHelper.CreateTestCertificate("CN=Active Certificate 1");
        var cert2 = TestCertificateHelper.CreateTestCertificate("CN=Active Certificate 2");
        var cert3 = TestCertificateHelper.CreateTestCertificate("CN=Inactive Certificate");

        await service.RecordCertificateUsageAsync(cert1, cancellationToken);
        await service.RecordCertificateUsageAsync(cert2, cancellationToken);
        await service.RecordCertificateUsageAsync(cert3, cancellationToken);

        // Deactivate one
        var fingerprint3 = TestCertificateHelper.ComputeSha256Fingerprint(cert3);
        await service.DeactivateCertificateAsync(fingerprint3, CertificateHashAlgorithm.Sha256, "Test", cancellationToken);

        // Act
        var certificates = await service.GetActiveCertificatesAsync(cancellationToken);

        // Assert
        Assert.Equal(2, certificates.Count);
        Assert.All(certificates, c => Assert.True(c.IsActive));
        Assert.Contains(certificates, c => c.Subject == "CN=Active Certificate 1");
        Assert.Contains(certificates, c => c.Subject == "CN=Active Certificate 2");
        Assert.DoesNotContain(certificates, c => c.Subject == "CN=Inactive Certificate");
    }

    [Fact]
    public async Task DeactivateCertificateAsync_WithNullNotes_DoesNotSetNotes()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);

        // Act
        await service.DeactivateCertificateAsync(fingerprint, CertificateHashAlgorithm.Sha256, null, cancellationToken);

        // Assert
        var deactivated = await _context.RepositorySigningCertificates.FirstAsync(cancellationToken);
        Assert.False(deactivated.IsActive);
        Assert.Null(deactivated.Notes);
    }

    [Fact]
    public async Task DeactivateCertificateAsync_WithEmptyNotes_DoesNotSetNotes()
    {
        var cancellationToken = CurrentCancellationToken;
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate, cancellationToken);

        var fingerprint = TestCertificateHelper.ComputeSha256Fingerprint(certificate);

        // Act
        await service.DeactivateCertificateAsync(fingerprint, CertificateHashAlgorithm.Sha256, string.Empty, cancellationToken);

        // Assert
        var deactivated = await _context.RepositorySigningCertificates.FirstAsync(cancellationToken);
        Assert.False(deactivated.IsActive);
        Assert.Null(deactivated.Notes);
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
        public string GetCertificateDownloadUrl(string fingerprint) => $"https://example.com/v3/certificates/{fingerprint}.crt";

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
