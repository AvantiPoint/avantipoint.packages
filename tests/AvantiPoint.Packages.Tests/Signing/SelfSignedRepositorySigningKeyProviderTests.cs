using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class SelfSignedRepositorySigningKeyProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;
    private readonly string _tempDirectory;
    private readonly FileStorageService _storage;
    private readonly RepositorySigningCertificateService _certificateService;
    private readonly TestUrlGenerator _urlGenerator;

    public SelfSignedRepositorySigningKeyProviderTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SqliteContext(options);
        _context.Database.EnsureCreated();

        // Create temporary directory for file storage
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        var storageOptions = new FileSystemStorageOptions { Path = _tempDirectory };
        var storageOptionsSnapshot = new MockOptionsSnapshot<FileSystemStorageOptions>(storageOptions);
        _storage = new FileStorageService(storageOptionsSnapshot);
        _urlGenerator = new TestUrlGenerator();
        _certificateService = new RepositorySigningCertificateService(_context, NullLogger<RepositorySigningCertificateService>.Instance, TimeProvider.System, _urlGenerator);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();

        // Clean up temporary directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private SelfSignedRepositorySigningKeyProvider CreateProvider(
        string? serverName = "test-server.example.com",
        string? subjectName = null,
        string? password = null,
        string certificatePath = "certs/repository-signing.pfx")
    {
        var selfSignedOptions = new SelfSignedCertificateOptions
        {
            SubjectName = subjectName,
            Organization = "Test Organization",
            OrganizationalUnit = "Test OU",
            Country = "US",
            KeySize = RsaKeySize.KeySize2048, // Use smaller key size for faster tests
            HashAlgorithm = "SHA256",
            ValidityInDays = 365,
            CertificatePath = certificatePath
        };

        var signingOptions = new SigningOptions
        {
            Mode = SigningMode.SelfSigned,
            SelfSigned = selfSignedOptions,
            CertificatePassword = password
        };

        var feedOptions = new PackageFeedOptions
        {
            Shield = serverName != null ? new ShieldOptions { ServerName = serverName } : null
        };

        var validationHelper = new CertificateValidationHelper(TimeProvider.System);
        return new SelfSignedRepositorySigningKeyProvider(
            Options.Create(signingOptions),
            Options.Create(feedOptions),
            _storage,
            _certificateService,
            NullLogger<SelfSignedRepositorySigningKeyProvider>.Instance,
            TimeProvider.System,
            validationHelper);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithNoExistingCertificate_GeneratesNewCertificate()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert
        Assert.NotNull(certificate);
        Assert.True(certificate.HasPrivateKey);
        Assert.Contains("CN=test-server.example.com", certificate.Subject);
        Assert.Contains("O=Test Organization", certificate.Subject);
        Assert.Contains("OU=Test OU", certificate.Subject);
        Assert.Contains("C=US", certificate.Subject);

        // Verify certificate was saved to storage
        var savedStream = await _storage.GetAsync("certs/repository-signing.pfx", CancellationToken.None);
        Assert.NotNull(savedStream);
        savedStream.Dispose();
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithExistingValidCertificate_ReusesCertificate()
    {
        // Arrange
        var provider1 = CreateProvider();
        var firstCertificate = await provider1.GetSigningCertificateAsync();
        var firstThumbprint = firstCertificate.Thumbprint;

        // Act - Create new provider instance (simulating app restart)
        var provider2 = CreateProvider();
        var secondCertificate = await provider2.GetSigningCertificateAsync();

        // Assert
        Assert.NotNull(secondCertificate);
        Assert.Equal(firstThumbprint, secondCertificate.Thumbprint);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithExpiredCertificate_GeneratesNewCertificate()
    {
        // Arrange - Create and save an expired certificate manually
        var expiredCert = TestCertificateHelper.CreateExpiredCertificate("CN=test-server.example.com, O=Test Organization, OU=Test OU, C=US");
        var pfxBytes = expiredCert.Export(X509ContentType.Pfx);
        using var stream = new MemoryStream(pfxBytes);
        await _storage.PutAsync("certs/repository-signing.pfx", stream, "application/x-pkcs12", CancellationToken.None);
        expiredCert.Dispose();

        var provider = CreateProvider();

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert - Should generate new certificate
        Assert.NotNull(certificate);
        Assert.NotEqual(expiredCert.Thumbprint, certificate.Thumbprint);
        Assert.True(certificate.HasPrivateKey);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithCertificateExpiringWithin5Minutes_GeneratesNewCertificate()
    {
        // Arrange - Create a certificate that expires in 3 minutes
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test-server.example.com, O=Test Organization, OU=Test OU, C=US",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var notBefore = DateTimeOffset.UtcNow.AddDays(-30);
        var notAfter = DateTimeOffset.UtcNow.AddMinutes(3); // Expires in 3 minutes
        var expiringCert = request.CreateSelfSigned(notBefore, notAfter);

        var pfxBytes = expiringCert.Export(X509ContentType.Pfx);
        using var stream = new MemoryStream(pfxBytes);
        await _storage.PutAsync("certs/repository-signing.pfx", stream, "application/x-pkcs12", CancellationToken.None);
        expiringCert.Dispose();

        var provider = CreateProvider();

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert - Should generate new certificate
        Assert.NotNull(certificate);
        Assert.True(certificate.HasPrivateKey);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithCertificateExpiringWithin7Days_GeneratesNewCertificate()
    {
        // Arrange - Create a certificate that expires in 5 days (within 7-day rotation window)
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test-server.example.com, O=Test Organization, OU=Test OU, C=US",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var notBefore = DateTimeOffset.UtcNow.AddDays(-360);
        var notAfter = DateTimeOffset.UtcNow.AddDays(5); // Expires in 5 days
        var expiringCert = request.CreateSelfSigned(notBefore, notAfter);

        var pfxBytes = expiringCert.Export(X509ContentType.Pfx);
        using var stream = new MemoryStream(pfxBytes);
        await _storage.PutAsync("certs/repository-signing.pfx", stream, "application/x-pkcs12", CancellationToken.None);
        expiringCert.Dispose();

        var provider = CreateProvider();

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert - Should generate new certificate (rotation)
        Assert.NotNull(certificate);
        Assert.True(certificate.HasPrivateKey);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithMismatchedSubjectName_GeneratesNewCertificate()
    {
        // Arrange - Create and save a certificate with different subject
        var firstProvider = CreateProvider(serverName: "server1.example.com");
        var firstCert = await firstProvider.GetSigningCertificateAsync();
        var firstThumbprint = firstCert.Thumbprint;

        // Act - Create provider with different server name
        var secondProvider = CreateProvider(serverName: "server2.example.com");
        var secondCert = await secondProvider.GetSigningCertificateAsync();

        // Assert - Should generate new certificate with new subject
        Assert.NotNull(secondCert);
        Assert.NotEqual(firstThumbprint, secondCert.Thumbprint);
        Assert.Contains("CN=server2.example.com", secondCert.Subject);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithMismatchedKeySize_GeneratesNewCertificate()
    {
        // Arrange - Create and save a certificate with 2048-bit key
        var provider1 = CreateProvider();
        var firstCert = await provider1.GetSigningCertificateAsync();
        var firstThumbprint = firstCert.Thumbprint;

        // Act - Create provider with different key size
        var selfSignedOptions = new SelfSignedCertificateOptions
        {
            Organization = "Test Organization",
            OrganizationalUnit = "Test OU",
            Country = "US",
            KeySize = RsaKeySize.KeySize4096, // Different key size
            HashAlgorithm = "SHA256",
            ValidityInDays = 365,
            CertificatePath = "certs/repository-signing.pfx"
        };

        var signingOptions = new SigningOptions
        {
            Mode = SigningMode.SelfSigned,
            SelfSigned = selfSignedOptions
        };

        var feedOptions = new PackageFeedOptions
        {
            Shield = new ShieldOptions { ServerName = "test-server.example.com" }
        };

        var validationHelper = new CertificateValidationHelper(TimeProvider.System);
        var provider2 = new SelfSignedRepositorySigningKeyProvider(
            Options.Create(signingOptions),
            Options.Create(feedOptions),
            _storage,
            _certificateService,
            NullLogger<SelfSignedRepositorySigningKeyProvider>.Instance,
            TimeProvider.System,
            validationHelper);

        var secondCert = await provider2.GetSigningCertificateAsync();

        // Assert - Should generate new certificate with new key size
        Assert.NotNull(secondCert);
        Assert.NotEqual(firstThumbprint, secondCert.Thumbprint);
        using var rsa = secondCert.GetRSAPublicKey();
        Assert.NotNull(rsa);
        Assert.Equal(4096, rsa.KeySize);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithExplicitSubjectName_UsesExplicitSubjectName()
    {
        // Arrange
        var explicitSubject = "CN=Custom Subject, O=Custom Org, C=CA";
        var provider = CreateProvider(subjectName: explicitSubject);

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert
        Assert.NotNull(certificate);
        Assert.Equal(explicitSubject, certificate.Subject);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithPasswordProtectedCertificate_SavesAndLoadsWithPassword()
    {
        // Arrange
        var password = "test-password-123";
        var provider = CreateProvider(password: password);

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert
        Assert.NotNull(certificate);
        Assert.True(certificate.HasPrivateKey);

        // Verify we can reload it with password
        var provider2 = CreateProvider(password: password);
        var reloadedCert = await provider2.GetSigningCertificateAsync();
        Assert.NotNull(reloadedCert);
        Assert.Equal(certificate.Thumbprint, reloadedCert.Thumbprint);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_WithNullServerName_UsesDefaultCN()
    {
        // Arrange
        var provider = CreateProvider(serverName: null);

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert
        Assert.NotNull(certificate);
        Assert.Contains("CN=AvantiPoint Packages", certificate.Subject);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_CachesCertificate_ReturnsSameInstance()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var firstCall = await provider.GetSigningCertificateAsync();
        var secondCall = await provider.GetSigningCertificateAsync();

        // Assert
        Assert.Same(firstCall, secondCall);
    }

    [Fact]
    public async Task GetSigningCertificateAsync_RecordsCertificateUsageInDatabase()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert
        var saved = await _context.RepositorySigningCertificates
            .FirstOrDefaultAsync(c => c.Fingerprint == TestCertificateHelper.ComputeSha256Fingerprint(certificate));
        Assert.NotNull(saved);
        Assert.Equal(certificate.Subject, saved.Subject);
        Assert.Equal(CertificateHashAlgorithm.Sha256, saved.HashAlgorithm);
    }

    [Fact]
    public void Constructor_WithNullSigningOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var validationHelper = new CertificateValidationHelper(TimeProvider.System);
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SelfSignedRepositorySigningKeyProvider(
            null!,
            Options.Create(new PackageFeedOptions()),
            _storage,
            _certificateService,
            NullLogger<SelfSignedRepositorySigningKeyProvider>.Instance,
            TimeProvider.System,
            validationHelper));
    }

    [Fact]
    public void Constructor_WithNullSelfSignedOptions_ThrowsInvalidOperationException()
    {
        // Arrange
        var signingOptions = new SigningOptions
        {
            Mode = SigningMode.SelfSigned,
            SelfSigned = null
        };

        // Arrange
        var validationHelper = new CertificateValidationHelper(TimeProvider.System);
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new SelfSignedRepositorySigningKeyProvider(
            Options.Create(signingOptions),
            Options.Create(new PackageFeedOptions()),
            _storage,
            _certificateService,
            NullLogger<SelfSignedRepositorySigningKeyProvider>.Instance,
            TimeProvider.System,
            validationHelper));
    }

    [Fact]
    public void Constructor_WithNullStorage_ThrowsArgumentNullException()
    {
        // Arrange
        var signingOptions = new SigningOptions
        {
            Mode = SigningMode.SelfSigned,
            SelfSigned = new SelfSignedCertificateOptions
            {
                Organization = "Test Org",
                CertificatePath = "test.pfx"
            }
        };

        // Arrange
        var validationHelper = new CertificateValidationHelper(TimeProvider.System);
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SelfSignedRepositorySigningKeyProvider(
            Options.Create(signingOptions),
            Options.Create(new PackageFeedOptions()),
            null!,
            _certificateService,
            NullLogger<SelfSignedRepositorySigningKeyProvider>.Instance,
            TimeProvider.System,
            validationHelper));
    }

    [Theory]
    [InlineData(RsaKeySize.KeySize2048)]
    [InlineData(RsaKeySize.KeySize3072)]
    [InlineData(RsaKeySize.KeySize4096)]
    public async Task GetSigningCertificateAsync_WithDifferentKeySizes_GeneratesCertificateWithCorrectKeySize(RsaKeySize keySize)
    {
        // Arrange
        var selfSignedOptions = new SelfSignedCertificateOptions
        {
            Organization = "Test Organization",
            KeySize = keySize,
            HashAlgorithm = "SHA256",
            ValidityInDays = 365,
            CertificatePath = "certs/repository-signing.pfx"
        };

        var signingOptions = new SigningOptions
        {
            Mode = SigningMode.SelfSigned,
            SelfSigned = selfSignedOptions
        };

        var feedOptions = new PackageFeedOptions
        {
            Shield = new ShieldOptions { ServerName = "test-server.example.com" }
        };

        var validationHelper = new CertificateValidationHelper(TimeProvider.System);
        var provider = new SelfSignedRepositorySigningKeyProvider(
            Options.Create(signingOptions),
            Options.Create(feedOptions),
            _storage,
            _certificateService,
            NullLogger<SelfSignedRepositorySigningKeyProvider>.Instance,
            TimeProvider.System,
            validationHelper);

        // Act
        var certificate = await provider.GetSigningCertificateAsync();

        // Assert
        Assert.NotNull(certificate);
        using var rsa = certificate.GetRSAPublicKey();
        Assert.NotNull(rsa);
        Assert.Equal((int)keySize, rsa.KeySize);
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

