using AvantiPoint.Packages.Host.Admin.Services.Secrets;
using Microsoft.AspNetCore.DataProtection;

namespace AvantiPoint.Packages.Host.Admin.Tests.Secrets;

public sealed class DataProtectionSecretProtectorTests
{
    private static DataProtectionSecretProtector CreateProtector() =>
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Protect_RoundTrips()
    {
        var protector = CreateProtector();

        var stored = protector.Protect("nuget-org-api-key");

        Assert.NotNull(stored);
        Assert.StartsWith("dpv1:", stored);
        Assert.NotEqual("nuget-org-api-key", stored);
        Assert.Equal("nuget-org-api-key", protector.Unprotect(stored));
    }

    [Fact]
    public void Protect_IsIdempotent_OnProtectedValues()
    {
        var protector = CreateProtector();

        var once = protector.Protect("secret");
        var twice = protector.Protect(once);

        Assert.Equal(once, twice);
        Assert.Equal("secret", protector.Unprotect(twice));
    }

    [Fact]
    public void Unprotect_PassesThroughLegacyPlaintext()
    {
        var protector = CreateProtector();

        Assert.Equal("legacy-plaintext-token", protector.Unprotect("legacy-plaintext-token"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullAndEmpty_PassThroughEverywhere(string? value)
    {
        var protector = CreateProtector();

        Assert.Equal(value, protector.Protect(value));
        Assert.Equal(value, protector.Unprotect(value));
        Assert.True(protector.IsProtected(value));
    }

    [Fact]
    public void IsProtected_DistinguishesPlaintextFromCiphertext()
    {
        var protector = CreateProtector();

        Assert.False(protector.IsProtected("plaintext"));
        Assert.True(protector.IsProtected(protector.Protect("plaintext")));
    }

    [Fact]
    public void HandlesLegacyPlaintext_ThatCoincidentallyStartsWithThePrefix()
    {
        var protector = CreateProtector();
        const string plaintextWithPrefixCollision = "dpv1:this-is-not-actually-encrypted";

        // Not real ciphertext, so it must be recognized as plaintext, not skipped as "already protected".
        Assert.False(protector.IsProtected(plaintextWithPrefixCollision));
        Assert.Equal(plaintextWithPrefixCollision, protector.Unprotect(plaintextWithPrefixCollision));

        // Protecting it must actually encrypt it (not treat it as a no-op), and the result
        // must round-trip back to the original value afterward.
        var stored = protector.Protect(plaintextWithPrefixCollision);
        Assert.NotEqual(plaintextWithPrefixCollision, stored);
        Assert.True(protector.IsProtected(stored));
        Assert.Equal(plaintextWithPrefixCollision, protector.Unprotect(stored));
    }
}
