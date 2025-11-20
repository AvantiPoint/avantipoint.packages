using System;
using System.Linq;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class RepositorySigningCertificateServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;

    public RepositorySigningCertificateServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SqliteContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RecordCertificateUsageAsync_WithNewCertificate_CreatesNewRecord()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");

        // Act
        await service.RecordCertificateUsageAsync(certificate);

        // Assert
        var saved = await _context.RepositorySigningCertificates.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(TestCertificateHelper.ComputeSha256Fingerprint(certificate), saved.Sha256Fingerprint);
        Assert.Equal(certificate.Subject, saved.Subject);
        Assert.Equal(certificate.Issuer, saved.Issuer);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task RecordCertificateUsageAsync_WithExistingCertificate_UpdatesLastUsed()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");

        // First usage
        await service.RecordCertificateUsageAsync(certificate);
        var firstUsage = await _context.RepositorySigningCertificates.FirstAsync();
        var originalFirstUsed = firstUsage.FirstUsed;
        var originalLastUsed = firstUsage.LastUsed;

        // Wait a moment to ensure time difference
        await Task.Delay(100);

        // Act - Second usage
        await service.RecordCertificateUsageAsync(certificate);

        // Assert
        var updated = await _context.RepositorySigningCertificates.FirstAsync();
        Assert.Equal(originalFirstUsed, updated.FirstUsed); // FirstUsed should not change
        Assert.True(updated.LastUsed > originalLastUsed); // LastUsed should be updated
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_ReturnsOnlyActiveCertificates()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance);

        var cert1 = TestCertificateHelper.CreateTestCertificate("CN=Active Certificate 1");
        var cert2 = TestCertificateHelper.CreateTestCertificate("CN=Active Certificate 2");
        var cert3 = TestCertificateHelper.CreateTestCertificate("CN=Inactive Certificate");

        await service.RecordCertificateUsageAsync(cert1);
        await service.RecordCertificateUsageAsync(cert2);
        await service.RecordCertificateUsageAsync(cert3);

        // Deactivate cert3
        var sha256 = TestCertificateHelper.ComputeSha256Fingerprint(cert3);
        await service.DeactivateCertificateAsync(sha256, "Test deactivation");

        // Act
        var activeCertificates = await service.GetActiveCertificatesAsync();

        // Assert
        Assert.Equal(2, activeCertificates.Count);
        Assert.DoesNotContain(activeCertificates, c => c.Subject == "CN=Inactive Certificate");
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_ExcludesExpiredCertificatesNotRecentlyUsed()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance);

        // Create and record an expired certificate that was last used 100 days ago
        var expiredCert = TestCertificateHelper.CreateExpiredCertificate();
        await service.RecordCertificateUsageAsync(expiredCert);

        var saved = await _context.RepositorySigningCertificates
            .FirstAsync(c => c.Subject == expiredCert.Subject);
        saved.LastUsed = DateTime.UtcNow.AddDays(-100);
        await _context.SaveChangesAsync(default);

        // Act
        var activeCertificates = await service.GetActiveCertificatesAsync();

        // Assert
        Assert.Empty(activeCertificates);
    }

    [Fact]
    public async Task GetActiveCertificatesAsync_IncludesExpiredCertificatesRecentlyUsed()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance);

        // Create and record an expired certificate that was last used 30 days ago
        var expiredCert = TestCertificateHelper.CreateExpiredCertificate();
        await service.RecordCertificateUsageAsync(expiredCert);

        var saved = await _context.RepositorySigningCertificates
            .FirstAsync(c => c.Subject == expiredCert.Subject);
        saved.LastUsed = DateTime.UtcNow.AddDays(-30); // Within 90-day grace period
        await _context.SaveChangesAsync(default);

        // Act
        var activeCertificates = await service.GetActiveCertificatesAsync();

        // Assert
        Assert.Single(activeCertificates);
    }

    [Fact]
    public async Task DeactivateCertificateAsync_MarksCertificateAsInactive()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance);

        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        await service.RecordCertificateUsageAsync(certificate);

        var sha256 = TestCertificateHelper.ComputeSha256Fingerprint(certificate);

        // Act
        await service.DeactivateCertificateAsync(sha256, "Certificate compromised");

        // Assert
        var deactivated = await _context.RepositorySigningCertificates.FirstAsync();
        Assert.False(deactivated.IsActive);
        Assert.Equal("Certificate compromised", deactivated.Notes);
    }

    [Fact]
    public async Task DeactivateCertificateAsync_WithNonExistentCertificate_DoesNotThrow()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance);

        // Act & Assert - Should not throw
        await service.DeactivateCertificateAsync("nonexistent-fingerprint", "test");
    }

    [Fact]
    public async Task RecordCertificateUsageAsync_StoresAllFingerprintTypes()
    {
        // Arrange
        var service = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance);
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");

        // Act
        await service.RecordCertificateUsageAsync(certificate);

        // Assert
        var saved = await _context.RepositorySigningCertificates.FirstAsync();
        Assert.NotNull(saved.Sha256Fingerprint);
        Assert.NotNull(saved.Sha384Fingerprint);
        Assert.NotNull(saved.Sha512Fingerprint);
        Assert.Equal(64, saved.Sha256Fingerprint.Length); // 32 bytes * 2 hex chars
        Assert.Equal(96, saved.Sha384Fingerprint.Length); // 48 bytes * 2 hex chars
        Assert.Equal(128, saved.Sha512Fingerprint.Length); // 64 bytes * 2 hex chars
    }
}
