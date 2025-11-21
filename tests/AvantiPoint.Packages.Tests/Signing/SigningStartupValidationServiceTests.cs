using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class SigningStartupValidationServiceTests
{
    private readonly Mock<ILogger<SigningStartupValidationService>> _loggerMock;
    private readonly TestTimeProvider _timeProvider;
    private readonly CertificateValidationHelper _validationHelper;

    public SigningStartupValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<SigningStartupValidationService>>();
        _timeProvider = new TestTimeProvider();
        _validationHelper = new CertificateValidationHelper(_timeProvider);
    }

    private SigningStartupValidationService CreateService(IRepositorySigningKeyProvider signingKeyProvider)
    {
        return new SigningStartupValidationService(
            signingKeyProvider,
            _loggerMock.Object,
            _timeProvider,
            _validationHelper);
    }

    [Fact]
    public async Task StartAsync_WithNullSigningKeyProvider_SkipsValidation()
    {
        // Arrange
        var nullProvider = new NullSigningKeyProvider();
        var service = CreateService(nullProvider);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Repository signing is disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithNullCertificate_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockProvider = new Mock<IRepositorySigningKeyProvider>();
        mockProvider.Setup(x => x.GetSigningCertificateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((X509Certificate2?)null);

        var service = CreateService(mockProvider.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("Repository signing is enabled but no certificate could be loaded", ex.Message);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Repository signing is enabled but no certificate could be loaded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithCertificateWithoutPrivateKey_ThrowsInvalidOperationException()
    {
        // Arrange
        // Create a certificate without a private key (public key only)
        var certificate = TestCertificateHelper.CreateTestCertificate("CN=Test Certificate");
        // Export public key only
        var publicKeyBytes = certificate.Export(X509ContentType.Cert);
        var publicKeyOnlyCert = new X509Certificate2(publicKeyBytes);

        var mockProvider = new Mock<IRepositorySigningKeyProvider>();
        mockProvider.Setup(x => x.GetSigningCertificateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(publicKeyOnlyCert);

        var service = CreateService(mockProvider.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("does not have a private key", ex.Message);
        Assert.Contains("CN=Test Certificate", ex.Message);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("does not have a private key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithExpiredCertificate_ThrowsInvalidOperationException()
    {
        // Arrange
        var expiredCert = TestCertificateHelper.CreateExpiredCertificate("CN=Expired Certificate");
        var mockProvider = new Mock<IRepositorySigningKeyProvider>();
        mockProvider.Setup(x => x.GetSigningCertificateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredCert);

        var service = CreateService(mockProvider.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CN=Expired Certificate", ex.Message);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("expired")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithCertificateExpiringWithin5Minutes_ThrowsInvalidOperationException()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var cert = CreateCertificateWithDates(
            "CN=Expiring Certificate",
            notBefore: now.AddDays(-365),
            notAfter: now.AddMinutes(3)); // Expires in 3 minutes (less than 5-minute buffer)

        var mockProvider = new Mock<IRepositorySigningKeyProvider>();
        mockProvider.Setup(x => x.GetSigningCertificateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var service = CreateService(mockProvider.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("expires in", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5 minute", ex.Message);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("expires in")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithValidCertificate_LogsSuccess()
    {
        // Arrange
        var cert = TestCertificateHelper.CreateTestCertificate("CN=Valid Certificate");
        var mockProvider = new Mock<IRepositorySigningKeyProvider>();
        mockProvider.Setup(x => x.GetSigningCertificateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var service = CreateService(mockProvider.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Repository signing certificate validated successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithCertificateExpiringWithin30Days_LogsWarning()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var cert = CreateCertificateWithDates(
            "CN=Expiring Soon Certificate",
            notBefore: now.AddDays(-365),
            notAfter: now.AddDays(20)); // Expires in 20 days (within 30-day warning threshold)

        var mockProvider = new Mock<IRepositorySigningKeyProvider>();
        mockProvider.Setup(x => x.GetSigningCertificateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var service = CreateService(mockProvider.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("expires in") && v.ToString()!.Contains("days")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithCertificateExpiringAfter30Days_DoesNotLogWarning()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var cert = CreateCertificateWithDates(
            "CN=Valid Certificate",
            notBefore: now.AddDays(-365),
            notAfter: now.AddDays(60)); // Expires in 60 days (outside 30-day warning threshold)

        var mockProvider = new Mock<IRepositorySigningKeyProvider>();
        mockProvider.Setup(x => x.GetSigningCertificateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var service = CreateService(mockProvider.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("expires in")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_WithProviderException_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockProvider = new Mock<IRepositorySigningKeyProvider>();
        mockProvider.Setup(x => x.GetSigningCertificateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider error"));

        var service = CreateService(mockProvider.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("Failed to validate repository signing certificate during startup", ex.Message);
        Assert.NotNull(ex.InnerException);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to validate repository signing certificate")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        // Arrange
        var service = CreateService(new NullSigningKeyProvider());

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert - No exceptions thrown
    }

    private static X509Certificate2 CreateCertificateWithDates(
        string subjectName,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            subjectName,
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        return request.CreateSelfSigned(notBefore, notAfter);
    }
}

