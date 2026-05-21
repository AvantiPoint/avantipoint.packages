using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Packages.Host.Admin.Configuration;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Services.Tokens;

public sealed class HostTokenHasher(IOptions<HostSettings> settings) : IHostTokenHasher
{
    public (string Plaintext, string Prefix, string Hash) GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        var plaintext = Convert.ToHexString(bytes).ToLowerInvariant();
        var prefix = plaintext[..8];
        return (plaintext, prefix, Hash(plaintext));
    }

    public bool Verify(string plaintext, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash),
            Encoding.UTF8.GetBytes(Hash(plaintext)));

    private string Hash(string plaintext)
    {
        var pepper = settings.Value.TokenHashPepper ?? string.Empty;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext + pepper));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
