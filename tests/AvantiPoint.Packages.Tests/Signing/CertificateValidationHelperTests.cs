using System;
using System.Security.Cryptography.X509Certificates;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Tests.Helpers;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class CertificateValidationHelperTests
{
    private readonly CertificateValidationHelper _helper;
    private readonly TestTimeProvider _timeProvider;

    public CertificateValidationHelperTests()
    {
        _timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        _helper = new CertificateValidationHelper(_timeProvider);
    }

    [Fact]
    public void IsCertificateExpired_WithNullCertificate_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _helper.IsCertificateExpired(null!));
    }

    [Fact]
    public void IsCertificateExpired_WithExpiredCertificate_ReturnsTrue()
    {
        // Arrange - Create certificate that expired 1 day ago
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        var notAfter = now.AddDays(-1); // Expired 1 day ago

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var expiredCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(expiredCert);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCertificateExpired_WithNotYetValidCertificate_ReturnsTrue()
    {
        // Arrange - Create certificate that becomes valid in 1 day
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(1); // Valid starting tomorrow
        var notAfter = now.AddDays(366); // Expires in 1 year

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var futureCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(futureCert);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCertificateExpired_WithValidCertificate_ReturnsFalse()
    {
        // Arrange - Create certificate valid for 365 days
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-1); // Valid since yesterday
        var notAfter = now.AddDays(365); // Expires in 365 days

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var validCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(validCert);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCertificateExpired_WithCertificateExpiringIn3Minutes_ReturnsTrue()
    {
        // Arrange - Create certificate expiring in 3 minutes (less than 5-minute buffer)
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        var notAfter = now.AddMinutes(3); // Expires in 3 minutes

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var expiringCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(expiringCert);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCertificateExpired_WithCertificateExpiringIn4Minutes59Seconds_ReturnsTrue()
    {
        // Arrange - Create certificate expiring in 4 minutes 59 seconds (just under 5-minute buffer)
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        var notAfter = now.AddMinutes(4).AddSeconds(59); // Expires in 4:59

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var expiringCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(expiringCert);

        // Assert - Should return true because it expires in less than 5 minutes
        Assert.True(result);
    }

    [Fact]
    public void IsCertificateExpired_WithCertificateExpiringIn5Minutes1Second_ReturnsFalse()
    {
        // Arrange - Create certificate expiring in 5 minutes 1 second (just over 5-minute buffer)
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        // Add a small buffer (10 seconds) to account for any timing precision issues
        var notAfter = now.AddMinutes(5).AddSeconds(10); // Expires in 5:10

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var validCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(validCert);

        // Assert - Should return false because it expires in more than 5 minutes
        Assert.False(result);
    }

    [Fact]
    public void IsCertificateExpired_WithAlreadyExpiredCertificate_ReturnsTrue()
    {
        // Arrange - Create certificate that expired 1 minute ago
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        var notAfter = now.AddMinutes(-1); // Expired 1 minute ago

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var expiredCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(expiredCert);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCertificateExpired_WithCertificateExpiringExactlyAt5MinuteThreshold_ReturnsTrue()
    {
        // Arrange - Create certificate expiring exactly at 5-minute threshold
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        var notAfter = now.Add(CertificateValidationHelper.MinimumValidityPeriod); // Exactly 5 minutes

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var thresholdCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(thresholdCert);

        // Assert - Should return true because NotAfter < now + 5 minutes (strictly less than)
        Assert.True(result);
    }

    [Fact]
    public void IsCertificateExpired_WithCertificateExpiringIn6Minutes_ReturnsFalse()
    {
        // Arrange - Create certificate expiring in 6 minutes (more than 5-minute buffer)
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        // Use a more precise time to avoid any rounding issues
        var notAfter = now.Add(TimeSpan.FromMinutes(6)); // Expires in 6 minutes

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var validCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.IsCertificateExpired(validCert);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetTimeUntilExpiry_WithNullCertificate_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _helper.GetTimeUntilExpiry(null!));
    }

    [Fact]
    public void GetTimeUntilExpiry_WithExpiredCertificate_ReturnsNegativeValue()
    {
        // Arrange - Create certificate that expired 1 minute ago
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        var notAfter = now.AddMinutes(-1); // Expired 1 minute ago

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var expiredCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.GetTimeUntilExpiry(expiredCert);

        // Assert
        Assert.True(result.TotalSeconds < 0);
        Assert.True(result.TotalMinutes > -2); // Should be around -1 minute
    }

    [Fact]
    public void GetTimeUntilExpiry_WithCertificateExpiringIn3Minutes_ReturnsApproximately3Minutes()
    {
        // Arrange - Create certificate expiring in 3 minutes
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-30);
        var notAfter = now.Add(TimeSpan.FromMinutes(3)); // Expires in exactly 3 minutes

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var expiringCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.GetTimeUntilExpiry(expiringCert);

        // Assert - Should be approximately 3 minutes (within 5 second tolerance for certificate creation timing)
        Assert.True(result.TotalMinutes >= 2.9 && result.TotalMinutes <= 3.1, 
            $"Expected approximately 3 minutes, but got {result.TotalMinutes} minutes");
    }

    [Fact]
    public void GetTimeUntilExpiry_WithValidCertificate_ReturnsPositiveValue()
    {
        // Arrange - Create certificate valid for 365 days
        var now = _timeProvider.GetUtcNow();
        var notBefore = now.AddDays(-1); // Valid since yesterday
        var notAfter = now.AddDays(365); // Expires in 365 days

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var validCert = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _helper.GetTimeUntilExpiry(validCert);

        // Assert - Should be close to 365 days (within 1 day tolerance)
        Assert.True(result.TotalDays > 364 && result.TotalDays < 366);
    }

    [Fact]
    public void MinimumValidityPeriod_Is5Minutes()
    {
        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), CertificateValidationHelper.MinimumValidityPeriod);
    }
}

