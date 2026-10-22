using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Admin.Tests.Configuration;

public sealed class HostSecurityConfigurationTests
{
    [Fact]
    public void Development_AllowsLocalEvaluationDefaults() =>
        HostSecurityConfiguration.Validate(new ConfigurationBuilder().Build(), isDevelopment: true);

    [Theory]
    [InlineData("Host:TokenHashPepper", null)]
    [InlineData("Host:TokenHashPepper", "change-me-in-production")]
    [InlineData("Host:DataProtection:KeyPath", null)]
    [InlineData("Host:DataProtection:KeyRingIsExternallyProtected", "false")]
    [InlineData("Feed:Authentication:ApiKey", null)]
    [InlineData("Feed:Authentication:ApiKey", "REPLACE_ME")]
    public void Production_RejectsUnsafeSettingsWithoutLeakingSecret(string key, string? value)
    {
        var values = ValidSettings();
        values[key] = value;
        var exception = Assert.Throws<InvalidOperationException>(() => HostSecurityConfiguration.Validate(Config(values), false));
        Assert.DoesNotContain(values["Host:TokenHashPepper"] ?? "never-a-secret", exception.Message);
    }

    [Fact]
    public void Production_AllowsExplicitlyProtectedStorage() =>
        HostSecurityConfiguration.Validate(Config(ValidSettings()), false);

    [Fact]
    public void Production_AllowsConfiguredEntraInsteadOfBootstrapKey()
    {
        var values = ValidSettings();
        values.Remove("Feed:Authentication:ApiKey");
        values["Host:Authentication:Microsoft:ClientId"] = "test-client";
        values["Host:Authentication:Microsoft:ClientSecret"] = "test-secret";
        HostSecurityConfiguration.Validate(Config(values), false);
    }

    [Fact]
    public void CertificateRing_EncryptsKeysAndDecryptsAcrossRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ap-keyring-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Local test only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(2));
            var certificatePath = Path.Combine(directory, "test.pfx");
            File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, "test-only"));
            var values = ValidSettings();
            values["Host:DataProtection:KeyPath"] = directory;
            values["Host:DataProtection:CertificatePath"] = certificatePath;
            values["Host:DataProtection:CertificatePassword"] = "test-only";
            values["Host:DataProtection:KeyRingIsExternallyProtected"] = "false";
            var configuration = Config(values);
            HostSecurityConfiguration.Validate(configuration, false);
            string encrypted;
            using (var services = new ServiceCollection().AddLogging().AddHostAdminServices(configuration).BuildServiceProvider())
                encrypted = services.GetDataProtector("test").Protect("stored-upstream-credential");
            var keyXml = File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "key-*.xml")));
            Assert.Contains("encryptedSecret", keyXml);
            Assert.DoesNotContain("<masterKey", keyXml);
            HostSecurityConfiguration.Validate(configuration, false);
            using var restarted = new ServiceCollection().AddLogging().AddHostAdminServices(configuration).BuildServiceProvider();
            Assert.Equal("stored-upstream-credential", restarted.GetDataProtector("test").Unprotect(encrypted));

            File.WriteAllText(Path.Combine(directory, "key-legacy.xml"), "<key><masterKey><value>test</value></masterKey></key>");
            Assert.Contains("Do not delete keys", Assert.Throws<InvalidOperationException>(() =>
                HostSecurityConfiguration.Validate(configuration, false)).Message);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Host:TokenHashPepper"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        ["Feed:Authentication:ApiKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        ["Host:DataProtection:KeyPath"] = "/unused/ap-test-keyring",
        ["Host:DataProtection:KeyRingIsExternallyProtected"] = "true",
    };
    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
