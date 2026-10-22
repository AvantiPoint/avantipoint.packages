using System.Xml;
using Microsoft.Extensions.Configuration;

namespace AvantiPoint.Packages.Host.Admin.Configuration;

/// <summary>Rejects unsafe host defaults before creating directories or migrating databases.</summary>
public static class HostSecurityConfiguration
{
    public static void Validate(IConfiguration configuration, bool isDevelopment)
    {
        var authentication = configuration.GetSection("Host:Authentication").Get<HostAuthenticationOptions>() ?? new();
        var provider = HostAuthenticationResolver.TryResolve(authentication);
        if (isDevelopment) return;

        RequireSecret(configuration["Host:TokenHashPepper"], "Host:TokenHashPepper");
        var bootstrapKey = configuration["Feed:Authentication:ApiKey"];
        if (!string.IsNullOrWhiteSpace(bootstrapKey))
            RequireSecret(bootstrapKey, "Feed:Authentication:ApiKey");
        if (provider is null && string.IsNullOrWhiteSpace(bootstrapKey))
            throw new InvalidOperationException("Configure an external Host:Authentication provider or a Feed:Authentication:ApiKey before running outside Development.");

        var keyPath = configuration["Host:DataProtection:KeyPath"];
        if (string.IsNullOrWhiteSpace(keyPath))
            throw new InvalidOperationException("Host:DataProtection:KeyPath must identify durable, access-controlled key storage outside Development.");
        var externallyProtected = configuration.GetValue<bool>("Host:DataProtection:KeyRingIsExternallyProtected");
        if (string.IsNullOrWhiteSpace(configuration["Host:DataProtection:CertificatePath"]) && !externallyProtected)
            throw new InvalidOperationException("Configure Host:DataProtection:CertificatePath to encrypt the key ring, or explicitly set KeyRingIsExternallyProtected when the storage system supplies encryption and access control.");

        // Adding an encryptor only protects newly generated keys. Never silently present an old
        // plaintext ring as encrypted, and never destroy keys needed for stored credentials.
        if (!externallyProtected && Directory.Exists(keyPath))
        {
            foreach (var file in Directory.EnumerateFiles(keyPath, "key-*.xml"))
            {
                using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                while (reader.Read())
                    if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "masterKey")
                        throw new InvalidOperationException("The configured Data Protection ring contains existing unencrypted keys. Protect the existing storage externally or migrate the ring securely before starting. Do not delete keys: existing credentials depend on them.");
            }
        }
    }

    private static void RequireSecret(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 32
            || value.Contains("change-me", StringComparison.OrdinalIgnoreCase)
            || value.Contains("replace_me", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{key} must be a stable, deployment-specific random secret of at least 32 characters outside Development.");
    }
}
