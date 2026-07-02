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
    public void IsProtected_IsAMarkerCheckOnly_ItDoesNotAttemptDecryption()
    {
        // A value carrying the marker is treated as protected even though it is not real
        // ciphertext. This is deliberate - see the "key ring lost" tests below for why.
        var protector = CreateProtector();
        var markedButNotRealCiphertext = "dpv1:5b6d9e2a:not-real-ciphertext";

        Assert.True(protector.IsProtected(markedButNotRealCiphertext));
    }

    [Fact]
    public void Unprotect_Throws_WhenMarkedValueCannotBeDecrypted()
    {
        var protector = CreateProtector();
        var markedButNotRealCiphertext = "dpv1:5b6d9e2a:not-real-ciphertext";

        var ex = Assert.Throws<InvalidOperationException>(() => protector.Unprotect(markedButNotRealCiphertext));
        Assert.IsAssignableFrom<Exception>(ex.InnerException);
    }

    [Fact]
    public void Protect_NeverReEncrypts_AMarkedValue_EvenWhenItCannotCurrentlyBeDecrypted()
    {
        // Regression guard: if Protect() re-encrypted a marked-but-undecryptable value (treating
        // it as if it were plaintext), a temporarily wrong/missing key ring would permanently
        // destroy the original secret the moment anything called Protect() on it again (e.g. the
        // startup migration, or saving the same source from the admin UI).
        var protector = CreateProtector();
        var markedButNotRealCiphertext = "dpv1:5b6d9e2a:not-real-ciphertext";

        var result = protector.Protect(markedButNotRealCiphertext);

        Assert.Equal(markedButNotRealCiphertext, result);
    }

    [Fact]
    public void KeyRingLost_UnprotectThrows_ButOriginalCiphertextSurvivesAndRecoversLater()
    {
        // Simulates the real failure mode Codex flagged: a secret is encrypted with one key
        // ring, then the key ring is temporarily unavailable/misconfigured (different
        // IDataProtectionProvider instance here stands in for "wrong KeyPath/ApplicationName").
        var originalKeyRing = new EphemeralDataProtectionProvider();
        var protectorBeforeLoss = new DataProtectionSecretProtector(originalKeyRing);
        var stored = protectorBeforeLoss.Protect("upstream-api-key");

        var protectorDuringOutage = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

        // While the key ring is unavailable: decrypting throws (surfaced, not silently wrong)...
        Assert.Throws<InvalidOperationException>(() => protectorDuringOutage.Unprotect(stored));
        // ...and, critically, re-"protecting" it (e.g. a startup migration pass) must not mutate
        // or destroy the original ciphertext.
        Assert.Equal(stored, protectorDuringOutage.Protect(stored));

        // Once the correct key ring is restored, the original secret is still recoverable.
        var protectorAfterRecovery = new DataProtectionSecretProtector(originalKeyRing);
        Assert.Equal("upstream-api-key", protectorAfterRecovery.Unprotect(stored));
    }
}
