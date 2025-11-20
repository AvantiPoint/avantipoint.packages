using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AvantiPoint.Packages.Core.Signing;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class SigningOptionsTests
{
    [Fact]
    public void Validate_WhenModeIsNull_ReturnsNoErrors()
    {
        // Arrange
        var options = new SigningOptions { Mode = null };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenModeIsSelfSignedButOptionsAreMissing_ReturnsError()
    {
        // Arrange
        var options = new SigningOptions
        {
            Mode = SigningMode.SelfSigned,
            SelfSigned = null
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains("SelfSigned must be configured", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WhenModeIsStoredCertificateButOptionsAreMissing_ReturnsError()
    {
        // Arrange
        var options = new SigningOptions
        {
            Mode = SigningMode.StoredCertificate,
            StoredCertificate = null
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains("StoredCertificate must be configured", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WhenSelfSignedOptionsAreValid_ReturnsNoErrors()
    {
        // Arrange
        var options = new SigningOptions
        {
            Mode = SigningMode.SelfSigned,
            SelfSigned = new SelfSignedCertificateOptions
            {
                Organization = "Test Org",
                KeySize = 4096,
                HashAlgorithm = "SHA256",
                ValidityInDays = 3650
            }
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenSelfSignedOptionsAreInvalid_ReturnsErrors()
    {
        // Arrange
        var options = new SigningOptions
        {
            Mode = SigningMode.SelfSigned,
            SelfSigned = new SelfSignedCertificateOptions
            {
                Country = "USA", // Should be 2 letters
                KeySize = 4096,
                HashAlgorithm = "SHA256",
                ValidityInDays = 3650
            }
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage.Contains("Country"));
    }
}
