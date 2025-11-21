using System.Collections.Generic;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AvantiPoint.Packages.Tests.Signing;

public class PostConfigureSigningOptionsTests
{
    [Fact]
    public void PostConfigure_WithCertificatePasswordSecret_ResolvesPasswordFromConfiguration()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Signing:CertificatePasswordSecret", "MySecretKey" },
            { "MySecretKey", "resolved-password-123" }
        };
        var configuration = CreateConfiguration(configValues);
        var postConfigure = new PostConfigureSigningOptions(configuration);
        var options = new SigningOptions
        {
            CertificatePasswordSecret = "MySecretKey"
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert
        Assert.Equal("resolved-password-123", options.CertificatePassword);
    }

    [Fact]
    public void PostConfigure_WithCertificatePasswordSecretButMissingConfigKey_SetsEmptyString()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Signing:CertificatePasswordSecret", "MySecretKey" }
            // MySecretKey is not in configuration
        };
        var configuration = CreateConfiguration(configValues);
        var postConfigure = new PostConfigureSigningOptions(configuration);
        var options = new SigningOptions
        {
            CertificatePasswordSecret = "MySecretKey"
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert
        Assert.Equal(string.Empty, options.CertificatePassword);
    }

    [Fact]
    public void PostConfigure_WithNullCertificatePasswordSecret_SetsEmptyString()
    {
        // Arrange
        var configuration = CreateConfiguration(new Dictionary<string, string?>());
        var postConfigure = new PostConfigureSigningOptions(configuration);
        var options = new SigningOptions
        {
            CertificatePasswordSecret = null
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert
        Assert.Equal(string.Empty, options.CertificatePassword);
    }

    [Fact]
    public void PostConfigure_WithEmptyCertificatePasswordSecret_SetsEmptyString()
    {
        // Arrange
        var configuration = CreateConfiguration(new Dictionary<string, string?>());
        var postConfigure = new PostConfigureSigningOptions(configuration);
        var options = new SigningOptions
        {
            CertificatePasswordSecret = string.Empty
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert
        Assert.Equal(string.Empty, options.CertificatePassword);
    }

    [Fact]
    public void PostConfigure_WithWhitespaceCertificatePasswordSecret_SetsEmptyString()
    {
        // Arrange
        var configuration = CreateConfiguration(new Dictionary<string, string?>());
        var postConfigure = new PostConfigureSigningOptions(configuration);
        var options = new SigningOptions
        {
            CertificatePasswordSecret = "   "
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert
        Assert.Equal(string.Empty, options.CertificatePassword);
    }

    [Fact]
    public void PostConfigure_WithNullOptions_DoesNotThrow()
    {
        // Arrange
        var configuration = CreateConfiguration(new Dictionary<string, string?>());
        var postConfigure = new PostConfigureSigningOptions(configuration);

        // Act & Assert - Should not throw
        postConfigure.PostConfigure(null, null);
    }

    [Fact]
    public void PostConfigure_WorksWithEnvironmentVariables()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Signing:CertificatePasswordSecret", "CERT_PASSWORD" },
            { "CERT_PASSWORD", "env-password-456" }
        };
        var configuration = CreateConfiguration(configValues);
        var postConfigure = new PostConfigureSigningOptions(configuration);
        var options = new SigningOptions
        {
            CertificatePasswordSecret = "CERT_PASSWORD"
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert
        Assert.Equal("env-password-456", options.CertificatePassword);
    }

    [Fact]
    public void PostConfigure_WorksWithConfigurationSections()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Signing:CertificatePasswordSecret", "Secrets:CertificatePassword" },
            { "Secrets:CertificatePassword", "section-password-789" }
        };
        var configuration = CreateConfiguration(configValues);
        var postConfigure = new PostConfigureSigningOptions(configuration);
        var options = new SigningOptions
        {
            CertificatePasswordSecret = "Secrets:CertificatePassword"
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert
        Assert.Equal("section-password-789", options.CertificatePassword);
    }

    [Fact]
    public void PostConfigure_WithNullConfigurationValue_SetsEmptyString()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            { "Signing:CertificatePasswordSecret", "MySecretKey" },
            { "MySecretKey", null } // Configuration key exists but value is null
        };
        var configuration = CreateConfiguration(configValues);
        var postConfigure = new PostConfigureSigningOptions(configuration);
        var options = new SigningOptions
        {
            CertificatePasswordSecret = "MySecretKey"
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert
        Assert.Equal(string.Empty, options.CertificatePassword);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(values);
        return builder.Build();
    }
}

