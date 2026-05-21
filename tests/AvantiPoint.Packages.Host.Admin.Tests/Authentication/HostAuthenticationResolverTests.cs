using AvantiPoint.Packages.Host.Admin.Configuration;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authentication;

public class HostAuthenticationResolverTests
{
    [Fact]
    public void TryResolve_AutoDetectsMicrosoftFirst()
    {
        var options = new HostAuthenticationOptions
        {
            Microsoft = new HostMicrosoftOptions { ClientId = "ms", ClientSecret = "secret" },
            Google = new HostGoogleOptions { ClientId = "g", ClientSecret = "secret" },
        };

        Assert.Equal(HostAuthenticationProvider.MicrosoftAccount, HostAuthenticationResolver.TryResolve(options));
    }

    [Fact]
    public void TryResolve_FallsBackToGoogleWhenMicrosoftNotConfigured()
    {
        var options = new HostAuthenticationOptions
        {
            Google = new HostGoogleOptions { ClientId = "g", ClientSecret = "secret" },
        };

        Assert.Equal(HostAuthenticationProvider.Google, HostAuthenticationResolver.TryResolve(options));
    }

    [Fact]
    public void TryResolve_FallsBackToGitHub()
    {
        var options = new HostAuthenticationOptions
        {
            GitHub = new HostGitHubOptions { ClientId = "id", ClientSecret = "secret" },
        };

        Assert.Equal(HostAuthenticationProvider.GitHub, HostAuthenticationResolver.TryResolve(options));
    }

    [Fact]
    public void TryResolve_ReturnsNullWhenNothingConfigured()
    {
        var options = new HostAuthenticationOptions();
        Assert.Null(HostAuthenticationResolver.TryResolve(options));
    }

    [Fact]
    public void Resolve_ThrowsWhenNothingConfigured()
    {
        var options = new HostAuthenticationOptions();
        var ex = Assert.Throws<InvalidOperationException>(() => HostAuthenticationResolver.Resolve(options));
        Assert.Contains("Microsoft", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Google", ex.Message, StringComparison.Ordinal);
        Assert.Contains("GitHub", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolve_ThrowsWhenMicrosoftPartiallyConfigured()
    {
        var options = new HostAuthenticationOptions
        {
            Microsoft = new HostMicrosoftOptions { ClientId = "ms" },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => HostAuthenticationResolver.TryResolve(options));
        Assert.Contains("Microsoft", ex.Message, StringComparison.Ordinal);
        Assert.Contains("partially configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolve_ThrowsWhenGooglePartiallyConfigured()
    {
        var options = new HostAuthenticationOptions
        {
            Google = new HostGoogleOptions { ClientSecret = "secret" },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => HostAuthenticationResolver.TryResolve(options));
        Assert.Contains("Google", ex.Message, StringComparison.Ordinal);
    }
}
