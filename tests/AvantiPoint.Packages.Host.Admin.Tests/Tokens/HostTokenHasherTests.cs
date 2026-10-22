using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Tokens;

public class HostTokenHasherTests
{
    [Fact]
    public void GenerateToken_UsesAptPrefixAndIsNotGuidShaped()
    {
        var hasher = CreateHasher("pepper");
        var (plaintext, prefix, hash) = hasher.GenerateToken();

        Assert.StartsWith("apt_", plaintext, StringComparison.Ordinal);
        Assert.Equal(plaintext[..8], prefix);
        Assert.Equal(47, plaintext.Length);
        Assert.DoesNotMatch(new Regex("^[0-9a-fA-F]{32}$"), plaintext);
        Assert.DoesNotContain('+', plaintext);
        Assert.DoesNotContain('/', plaintext);
        Assert.DoesNotContain('=', plaintext);
        Assert.Matches(new Regex(@"^apt_[A-Za-z0-9_-]+$"), plaintext);
        Assert.True(hasher.Verify(plaintext, hash));
    }

    [Fact]
    public void Verify_AcceptsLegacy32HexTokens()
    {
        var hasher = CreateHasher("pepper");
        var legacy = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        Assert.Equal(32, legacy.Length);
        var hash = Sha256Hex(legacy + "pepper");
        Assert.True(hasher.Verify(legacy, hash));
        Assert.False(hasher.Verify(legacy + "x", hash));
    }

    [Fact]
    public void GenerateToken_ProducesUniqueValues()
    {
        var hasher = CreateHasher(string.Empty);
        var a = hasher.GenerateToken();
        var b = hasher.GenerateToken();
        Assert.NotEqual(a.Plaintext, b.Plaintext);
        Assert.NotEqual(a.Hash, b.Hash);
        Assert.NotEqual(a.Prefix, b.Prefix);
    }

    private static HostTokenHasher CreateHasher(string pepper) =>
        new(Options.Create(new HostSettings { TokenHashPepper = pepper }));

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
