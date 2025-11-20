using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using AvantiPoint.Packages.Core.Signing;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class StoredCertificateOptionsTests
{
    [Fact]
    public void Validate_WhenThumbprintIsProvided_WithStoreProperties_ReturnsNoErrors()
    {
        // Arrange
        var options = new StoredCertificateOptions
        {
            Thumbprint = "ABC123",
            StoreName = StoreName.My,
            StoreLocation = StoreLocation.CurrentUser
        };
        var context = new ValidationContext(options);
        var results = new System.Collections.Generic.List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        results.AddRange(options.Validate(context));

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenFilePathIsProvided_ReturnsNoErrors()
    {
        // Arrange
        var options = new StoredCertificateOptions
        {
            FilePath = "/path/to/cert.pfx",
            Password = "password123"
        };
        var context = new ValidationContext(options);
        var results = new System.Collections.Generic.List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        results.AddRange(options.Validate(context));

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenBothThumbprintAndFilePathAreProvided_ReturnsError()
    {
        // Arrange
        var options = new StoredCertificateOptions
        {
            Thumbprint = "ABC123",
            StoreName = StoreName.My,
            StoreLocation = StoreLocation.CurrentUser,
            FilePath = "/path/to/cert.pfx"
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains("Cannot specify both Thumbprint and FilePath", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WhenNeitherThumbprintNorFilePathProvided_ReturnsError()
    {
        // Arrange
        var options = new StoredCertificateOptions();
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains("Either Thumbprint (for certificate store) or FilePath", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WhenThumbprintProvidedButStoreNameMissing_ReturnsError()
    {
        // Arrange
        var options = new StoredCertificateOptions
        {
            Thumbprint = "ABC123",
            StoreLocation = StoreLocation.CurrentUser
            // StoreName is null
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains("StoreName is required when using Thumbprint", results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WhenThumbprintProvidedButStoreLocationMissing_ReturnsError()
    {
        // Arrange
        var options = new StoredCertificateOptions
        {
            Thumbprint = "ABC123",
            StoreName = StoreName.My
            // StoreLocation is null
        };
        var context = new ValidationContext(options);

        // Act
        var results = options.Validate(context).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains("StoreLocation is required when using Thumbprint", results[0].ErrorMessage);
    }
}
