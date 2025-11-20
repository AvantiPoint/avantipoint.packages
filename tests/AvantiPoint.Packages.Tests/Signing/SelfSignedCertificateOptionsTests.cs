using System.ComponentModel.DataAnnotations;
using System.Linq;
using AvantiPoint.Packages.Core.Signing;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class SelfSignedCertificateOptionsTests
{
    [Fact]
    public void Validate_WhenAllPropertiesAreValid_ReturnsNoErrors()
    {
        // Arrange
        var options = new SelfSignedCertificateOptions
        {
            SubjectName = "CN=Test",
            Organization = "Test Org",
            OrganizationalUnit = "Test OU",
            Country = "US",
            KeySize = 4096,
            HashAlgorithm = "SHA256",
            ValidityInDays = 3650,
            CertificatePath = "certs/test.pfx"
        };
        var context = new ValidationContext(options);
        var results = new System.Collections.Generic.List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenCountryCodeIsTooLong_ReturnsError()
    {
        // Arrange
        var options = new SelfSignedCertificateOptions
        {
            Organization = "Test Org",
            Country = "USA", // Should be 2 letters
            KeySize = 4096,
            HashAlgorithm = "SHA256",
            ValidityInDays = 3650
        };
        var context = new ValidationContext(options);
        var results = new System.Collections.Generic.List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SelfSignedCertificateOptions.Country)));
    }

    [Fact]
    public void Validate_WhenCountryCodeIsTooShort_ReturnsError()
    {
        // Arrange
        var options = new SelfSignedCertificateOptions
        {
            Organization = "Test Org",
            Country = "U",
            KeySize = 4096,
            HashAlgorithm = "SHA256",
            ValidityInDays = 3650
        };
        var context = new ValidationContext(options);
        var results = new System.Collections.Generic.List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SelfSignedCertificateOptions.Country)));
    }

    [Theory]
    [InlineData("US")]
    [InlineData("GB")]
    [InlineData("CA")]
    [InlineData("DE")]
    public void Validate_WhenCountryCodeIsValid_ReturnsNoError(string countryCode)
    {
        // Arrange
        var options = new SelfSignedCertificateOptions
        {
            Organization = "Test Org",
            Country = countryCode,
            KeySize = 4096,
            HashAlgorithm = "SHA256",
            ValidityInDays = 3650
        };
        var context = new ValidationContext(options);
        var results = new System.Collections.Generic.List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new SelfSignedCertificateOptions();

        // Assert
        Assert.Equal(4096, options.KeySize);
        Assert.Equal("SHA256", options.HashAlgorithm);
        Assert.Equal(3650, options.ValidityInDays);
        Assert.Equal("certs/repository-signing.pfx", options.CertificatePath);
    }

    [Fact]
    public void SubjectName_IsNullableByDefault()
    {
        // Arrange & Act
        var options = new SelfSignedCertificateOptions();

        // Assert
        Assert.Null(options.SubjectName);
    }
}
